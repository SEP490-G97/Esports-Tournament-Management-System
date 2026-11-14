using ETOMS_API.Middleware;
using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Repositories.Impl;
using ETOMS_API.Repositories.Interface;
using ETOMS_API.Services;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using ETOMS_API.Utils;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
            // Lỗi đọc JSON (sai kiểu, số tràn, JSON hỏng) của System.Text.Json đều kèm "Path: $..." — tiếng Anh.
            fieldErrors[field] = key.StartsWith("$") || raw.Contains("Path: $") || entry.Errors[0].Exception is not null
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
builder.Services.AddDbContext<EtomsDbContext>(options => options
    .UseMySQL(mysqlConnectionString)
    .AddInterceptors(new KeepConnectionOpenInterceptor()));

// Dò cột RegistrationLineup.GamePositionID (GRL-04) trước khi EF dựng model; xem Repositories/PendingSchema.cs.
PendingSchema.Detect(mysqlConnectionString);

builder.Services.AddScoped<IAdminAccountService, AdminAccountService>();
builder.Services.AddScoped<IOneTimeTokenService, OneTimeTokenService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddScoped(typeof(ETOMS_API.Repositories.Interface.IGenericRepository<>), typeof(ETOMS_API.Repositories.Impl.GenericRepository<>));
builder.Services.AddScoped<ETOMS_API.Repositories.Interface.IUnitOfWork, ETOMS_API.Repositories.Impl.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IProfileService, ETOMS_API.Services.Impl.ProfileService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IOrganizationService, ETOMS_API.Services.Impl.OrganizationService>();
builder.Services.AddScoped<ITournamentMarketingService, TournamentMarketingService>();
builder.Services.AddScoped<IRankingService, RankingService>();
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
builder.Services.AddScoped<IMatchScheduleService, MatchScheduleService>();
builder.Services.AddScoped<IGroupStandingService, GroupStandingService>();
builder.Services.AddScoped<IBracketViewService, BracketViewService>();
builder.Services.AddScoped<ITournamentSettingsService, TournamentSettingsService>();
builder.Services.AddScoped<ITournamentCancelService, TournamentCancelService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
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

app.UseDefaultFiles();
app.UseStaticFiles();
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
