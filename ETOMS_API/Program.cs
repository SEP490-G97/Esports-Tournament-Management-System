using ETOMS_API.Middleware;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Impl;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var connectionString = builder.Configuration.GetConnectionString("EtomsDatabase")
    ?? builder.Configuration.GetConnectionString("EtomsDb")
    ?? builder.Configuration["ETOMS_DB_CONNECTION"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình kết nối MySQL. Hãy đặt ConnectionStrings:EtomsDatabase hoặc biến môi trường ETOMS_DB_CONNECTION.");
}

// ---- Options ----
builder.Services.Configure<JwtOptions>(cfg.GetSection(JwtOptions.Section));
var jwt = cfg.GetSection(JwtOptions.Section).Get<JwtOptions>()!;
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AuthLinkOptions>(builder.Configuration.GetSection(AuthLinkOptions.SectionName));
builder.Services.Configure<SupportOptions>(builder.Configuration.GetSection(SupportOptions.SectionName));





// Email: chưa cấu hình đủ SMTP (tắt, hoặc thiếu mật khẩu) thì chỉ ghi nội dung mail ra log.
var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.CanSend) builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
else builder.Services.AddScoped<IEmailSender, LogEmailSender>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();

/* Rate limiting: giới hạn số lần gửi request (mail, login) trong 1 phút để chống spam, brute-force. 
 * Dùng IP làm key, nếu có proxy thì cần cấu hình X-Forwarded-For.
 *  Tạm thời chưa cần proxy, tất cả cấu hình trên Amazon EC2 đều chạy internal   
 */
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Phải có nội dung: 429 rỗng sẽ bị UseStatusCodePagesWithReExecute chuyển sang /errors/429.html
    // (không tồn tại) và thành 404 — trang đăng nhập/đăng ký không biết là bị giới hạn.
    o.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(new
    {
        code = "TOO_MANY_REQUESTS",
        message = "Bạn thao tác quá nhiều lần. Vui lòng đợi một phút rồi thử lại."
    }, ct));
    //login
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    //Mail
    o.AddPolicy("mail", ctx => RateLimitPartition.GetFixedWindowLimiter(
    ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
    _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});




builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg["Cors:Origin"]!)
    .AllowCredentials()
    .AllowAnyHeader().AllowAnyMethod()));

var mysqlConnectionStringBuilder = new MySqlConnectionStringBuilder(connectionString)
{
    CharacterSet = "utf8mb4"
};
var mysqlConnectionString = mysqlConnectionStringBuilder.ConnectionString;

// Chỉ Web API (JSON) — không còn Razor View engine. Giao diện là HTML/CSS/JS thuần trong
// wwwroot/, gọi các Controller này qua fetch().
// Dữ liệu gửi lên sai kiểu/quá lớn (vd số 99999999999 vào ô int) mặc định trả ProblemDetails tiếng
// Anh, không có "message" -> giao diện chỉ hiện "Lỗi 400". Trả về cùng dạng { message, fieldErrors }
// như ApiControllerBase, thông báo tiếng Việt, khóa field viết hoa chữ đầu để khớp data-field-error.
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var fieldErrors = new Dictionary<string, string>();
        foreach (var (key, entry) in ctx.ModelState)
        {
            if (entry.Errors.Count == 0) continue;
            var field = key.StartsWith("$.") ? key[2..] : key;
            // "The dto/req field is required." là lỗi kéo theo (tên tham số action, viết thường) — bỏ.
            if (field.Length == 0 || field == "$" || (!key.StartsWith("$") && char.IsLower(field[0]) && ctx.ModelState.Count > 1))
                continue;
            field = char.ToUpperInvariant(field[0]) + field[1..];
            var raw = entry.Errors[0].ErrorMessage;
            // Lỗi đọc dữ liệu của framework đều tiếng Anh: JSON sai kiểu/tràn ("Path: $..."), tham số URL/form sai
            // kiểu ("The value 'abc' is not valid for X", "The value '' is invalid").
            fieldErrors[field] = key.StartsWith("$") || raw.Contains("Path: $") || entry.Errors[0].Exception is not null
                || raw.Contains("is not valid") || raw.Contains("is invalid")
                ? "Giá trị không hợp lệ (sai định dạng hoặc quá lớn)."
                : raw.Contains("is required") ? "Trường này là bắt buộc." : raw;
        }
        var message = fieldErrors.Values.Any(v => v.StartsWith("Giá trị không hợp lệ"))
            ? "Dữ liệu chưa hợp lệ: có ô nhập sai định dạng hoặc giá trị quá lớn. Vui lòng kiểm tra lại."
            : fieldErrors.Count > 0 ? fieldErrors.Values.First() : "Dữ liệu gửi lên không hợp lệ.";
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { message, fieldErrors });
    };
});

builder.Services.AddOpenApi();

// SignalR cho thông báo/tỉ số trực tiếp (NOT-01, LIVE).
builder.Services.AddSignalR();

// Swagger UI để test API trực tiếp trên trình duyệt: /swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cache RAM ngắn hạn cho dữ liệu đọc nhiều/đổi chậm (vd trang chủ) — database ở xa
// (MySQL 15.135.76.150) nên mỗi round-trip mạng tốn ~1s, cache giúp các lượt tải lại
// trong vài chục giây không phải hỏi DB nữa.
builder.Services.AddMemoryCache();

// Nén phản hồi (Brotli/Gzip): JSON danh sách, HTML, CSS, JS nhỏ đi 4–8 lần khi máy chủ không chạy cùng máy người xem.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "image/svg+xml", "application/problem+json" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);

// Session (phục vụ stub đăng nhập demo — thay khi có AUTH thật).
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // API controllers (Controllers/Api/*) xác thực qua cookie session này nhưng KHÔNG có
    // antiforgery token như form MVC → bắt buộc Strict để trình duyệt không gửi cookie kèm
    // request POST/DELETE bắt nguồn từ site khác (chống CSRF).
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// EF Core — MySQL (MySql.EntityFrameworkCore của Oracle). Chuỗi kết nối lấy từ User Secrets
// hoặc biến môi trường ETOMS_DB_CONNECTION (KHÔNG commit).
// Đúng theo Report 4 (Software Design Specification): Controller → Service → IUnitOfWork
// (phát GenericRepository<T> theo entity, dùng chung 1 EtomsDbContext/request) → DB.
// Giữ 1 kết nối cho cả request thay vì mở/đóng quanh từng câu SQL (xem KeepConnectionOpenInterceptor).
// Mỗi request mượn một kết nối ĐANG MỞ từ WarmConnectionPool (xem lý do trong Repositories/WarmConnectionPool.cs):
// đo thực tế bỏ được ~2 lượt đi về database ở xa cho mỗi request. Tối đa 16 kết nối nhàn rỗi để không chiếm
// nhiều trong 151 kết nối của database dùng chung. KeepConnectionOpenInterceptor giữ kết nối mở suốt request.
var warmPool = new WarmConnectionPool(mysqlConnectionString);
builder.Services.AddSingleton(warmPool);
builder.Services.AddScoped<WarmConnectionLease>();
var keepOpen = new KeepConnectionOpenInterceptor();
builder.Services.AddDbContext<EtomsDbContext>((sp, options) => options
    .UseMySQL(sp.GetRequiredService<WarmConnectionLease>().Connection) // context KHÔNG sở hữu kết nối: hết request trả lại nhóm
    .AddInterceptors(keepOpen, sp.GetRequiredService<NotificationEmailInterceptor>()));   // + email cho người bật "Nhận thông báo qua email"
builder.Services.AddSingleton<NotificationEmailInterceptor>();
builder.Services.AddSingleton<NotificationMailQueue>();
builder.Services.AddHostedService<NotificationMailWorker>();

// Dò cột RegistrationLineup.GamePositionID (GRL-04) trước khi EF dựng model; xem Repositories/PendingSchema.cs.
PendingSchema.Detect(mysqlConnectionString);

builder.Services.AddScoped<IAdminAccountService, AdminAccountService>();
builder.Services.AddScoped<IOrganizerRequestService, OrganizerRequestService>();   // UC-43 xin làm BTC -> Admin cấp tài khoản BTC riêng
builder.Services.AddScoped<OrganizerHubService>();   // trang "Giải của tôi" của Ban tổ chức
builder.Services.AddScoped<IOneTimeTokenService, OneTimeTokenService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddScoped(typeof(ETOMS_API.Repositories.Interface.IGenericRepository<>), typeof(ETOMS_API.Repositories.Impl.GenericRepository<>));
builder.Services.AddScoped<ETOMS_API.Repositories.Interface.IUnitOfWork, ETOMS_API.Repositories.Impl.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IProfileService, ETOMS_API.Services.Impl.ProfileService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IOrganizationService, ETOMS_API.Services.Impl.OrganizationService>();
builder.Services.AddScoped<ITournamentMarketingService, TournamentMarketingService>();
builder.Services.AddScoped<IRankingService, RankingService>();
builder.Services.AddScoped<NewsFeedService>();
builder.Services.AddScoped<ITournamentRegistrationService, TournamentRegistrationService>();
builder.Services.AddSingleton<ETOMS_API.Utils.PrivateFileStorage>();
builder.Services.AddScoped<ICurrentUser, CurrentUserStub>();

// Service của từng module đăng ký ở đây.
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IBracketService, BracketService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IMatchOpsService, ETOMS_API.Services.Impl.MatchOpsService>();
builder.Services.AddHostedService<WaitlistExpirationWorker>();
builder.Services.AddHostedService<CompletedTournamentRankingWorker>();
builder.Services.AddHostedService<ReportTimeoutWorker>();
builder.Services.AddScoped<CheckInReminderService>();
builder.Services.AddHostedService<CheckInReminderWorker>();

builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountBanService, AccountBanService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.AddScoped<IRecruitmentService, RecruitmentService>();
builder.Services.AddScoped<ITournamentSetupService, TournamentSetupService>();
builder.Services.AddScoped<IRulebookService, RulebookService>();
builder.Services.AddScoped<ITournamentPublishService, TournamentPublishService>();
builder.Services.AddScoped<ITournamentDashboardService, TournamentDashboardService>();
builder.Services.AddScoped<IRefereeAssignmentService, RefereeAssignmentService>();
builder.Services.AddScoped<ITournamentCloneService, TournamentCloneService>();
builder.Services.AddScoped<IGameRuleService, GameRuleService>();
builder.Services.AddScoped<IGameMapService, GameMapService>();
builder.Services.AddScoped<IGamePositionService, GamePositionService>();
builder.Services.AddScoped<ILineupPositionService, LineupPositionService>();
builder.Services.AddScoped<ITournamentRulesetService, TournamentRulesetService>();
builder.Services.AddScoped<IMatchPickBanService, MatchPickBanService>();
builder.Services.AddScoped<IStageTransitionService, StageTransitionService>();
builder.Services.AddHostedService<StageTransitionWorker>();
builder.Services.AddScoped<IThirdPlaceService, ThirdPlaceService>();
builder.Services.AddHostedService<ThirdPlaceWorker>();
// Module PEN — UC-34/UC-35: thẻ phạt, ngưỡng loại tự động (Scheduler mỗi phút).
builder.Services.AddScoped<IDisciplineService, DisciplineService>();
builder.Services.AddHostedService<PenaltyThresholdWorker>();
// NUI-MAT-02 (tự đánh vắng, mỗi phút) và NUI-ADM-01 (dọn mã mời / token hết hạn, mỗi giờ).
builder.Services.AddHostedService<NoShowWorker>();
builder.Services.AddScoped<DataCleanupService>();
builder.Services.AddHostedService<DataCleanupWorker>();
builder.Services.AddScoped<IMatchScheduleService, MatchScheduleService>();
builder.Services.AddScoped<IGroupStandingService, GroupStandingService>();
builder.Services.AddScoped<IBracketViewService, BracketViewService>();
builder.Services.AddScoped<ITournamentSettingsService, TournamentSettingsService>();
builder.Services.AddScoped<ITournamentCancelService, TournamentCancelService>();
builder.Services.AddScoped<ITournamentLifecycleService, TournamentLifecycleService>();   // mục 28: bắt đầu / kết thúc giải, chưa đủ đội
builder.Services.AddHostedService<TournamentLifecycleWorker>();
builder.Services.AddScoped<ITournamentDirectoryService, TournamentDirectoryService>();
builder.Services.AddScoped<ILiveService, LiveService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Người dùng bấm Back / chuyển trang khi trang đang tải (góp ý 22/11 "back nhiều trang thì lỗi"): trình duyệt hủy yêu cầu,
// truy vấn đang chạy bị ngắt và ném OperationCanceledException (MySQL: MySqlException "Query execution was interrupted").
// Trước đây không ai bắt nên Visual Studio dừng vào code (user-unhandled) và log ghi đỏ dù ứng dụng không hỏng.
// Bắt ĐÚNG trường hợp yêu cầu đã bị hủy phía người dùng: không ghi lỗi, trả 499 (Client Closed Request). Lỗi thật vẫn đi tiếp.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex) when (ctx.RequestAborted.IsCancellationRequested && IsCancellation(ex))
    {
        if (!ctx.Response.HasStarted) ctx.Response.StatusCode = 499;
    }
});
static bool IsCancellation(Exception? ex)
{
    for (; ex is not null; ex = ex.InnerException)
        if (ex is OperationCanceledException
            || (ex is MySql.Data.MySqlClient.MySqlException m && m.Message.Contains("interrupted", StringComparison.OrdinalIgnoreCase)))
            return true;
    return false;
}

// Trang lỗi tĩnh (wwwroot/errors/*.html, KHÔNG dùng trang mặc định của trình duyệt):
//  - UseExceptionHandler: lỗi 500 chưa bắt được → render /errors/500.html
//  - UseStatusCodePagesWithReExecute: các mã lỗi (404, 403...) → render /errors/<mã>.html
// Loại trừ /api/*: API phải luôn trả JSON thật (kể cả lỗi), không bị ghi đè thành trang HTML.
// PHẢI đứng TRƯỚC UseStaticFiles — re-execute chỉ đi tiếp qua middleware đăng ký SAU nó,
// nên static-file middleware phải nằm sau để bị re-execute vào được.
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseExceptionHandler("/errors/500.html");
    branch.UseStatusCodePagesWithReExecute("/errors/{0}.html");
});

// Ảnh / tệp tải lên ở máy khác (18/11): lưu một bản trong DB chung (Utils/SharedFiles). Máy này chưa có tệp
// /uploads/... thì lấy từ DB về đĩa trước khi bộ phục vụ tệp tĩnh đọc. Tệp cũ của máy này được đưa lên DB chạy nền.
{
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    SharedFiles.Configure(mysqlConnectionString, webRoot,
        Path.Combine(app.Environment.ContentRootPath, "App_Data", "private-uploads"),
        app.Services.GetRequiredService<IHttpContextAccessor>(),
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SharedFiles"));
    var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads")) + Path.DirectorySeparatorChar;
    app.Use(async (ctx, next) =>
    {
        var path = ctx.Request.Path.Value;
        if (path is not null && HttpMethods.IsGet(ctx.Request.Method) && path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(Path.Combine(webRoot, Uri.UnescapeDataString(path).TrimStart('/')));
            if (full.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) && !File.Exists(full))
                await SharedFiles.RestoreAsync(full, ctx.RequestAborted);
        }
        await next();
    });
    var stopping = app.Lifetime.ApplicationStopping;
    app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(() => SharedFiles.BackfillAsync(stopping)));
}

// Không nén phản hồi của /auth (chứa token): tránh kiểu tấn công đoán bí mật qua độ dài bản nén (BREACH).
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/auth"), branch => branch.UseResponseCompression());
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // Trang .html không có ?v= như CSS/JS: thiếu Cache-Control thì trình duyệt tự giữ bản cũ một thời gian
    // (người dùng vẫn thấy giao diện trước khi sửa). "no-cache" = vẫn lưu nhưng luôn hỏi lại máy chủ
    // (ETag/Last-Modified) trước khi dùng, nên chỉ tốn một yêu cầu 304 nhẹ khi file không đổi.
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            ctx.Context.Response.Headers.CacheControl = "no-cache";
        // CSS/JS gắn ?v=... đổi phiên bản mỗi lần sửa → cho trình duyệt giữ hẳn 1 năm, khỏi hỏi lại máy chủ
        // mỗi lần chuyển trang (mỗi trang ~8 tệp).
        else if (ctx.Context.Request.Query.ContainsKey("v"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        // Ảnh tải lên có tên ngẫu nhiên (đổi ảnh = tên mới) → giữ 7 ngày; ảnh tĩnh của web (có thể bị thay cùng tên) giữ 1 giờ.
        // Trước đây không có Cache-Control nên mỗi trang danh sách (logo đội, nhà tài trợ…) lại hỏi lại máy chủ.
        else if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=604800";
        else if (ctx.Context.Request.Path.StartsWithSegments("/images"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=3600";
    }
});
app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<BanCheckMiddleware>();
app.UseAuthorization();

// API controllers (attribute routing [Route("api/...")]) — không còn route quy ước MVC.
app.MapControllers();

// app.MapHub<...>("/hubs/...");  // đăng ký SignalR Hub cụ thể khi tạo Hub

app.Run();
