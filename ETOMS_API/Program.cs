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

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
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
builder.Services.AddControllers();

builder.Services.AddOpenApi();

// SignalR cho thông báo/tỉ số trực tiếp (NOT-01, LIVE).
builder.Services.AddSignalR();

// Swagger UI để test API trực tiếp trên trình duyệt: /swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AutoMapper: quét toàn bộ Profile trong assembly.
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

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
builder.Services.AddDbContext<EtomsDbContext>(options => options.UseMySQL(mysqlConnectionString));

// Dò cột RegistrationLineup.GamePositionID (GRL-04) trước khi EF dựng model; xem Repositories/PendingSchema.cs.
PendingSchema.Detect(mysqlConnectionString);

builder.Services.AddScoped(typeof(ETOMS_API.Repositories.Interface.IGenericRepository<>), typeof(ETOMS_API.Repositories.Impl.GenericRepository<>));
builder.Services.AddScoped<ETOMS_API.Repositories.Interface.IUnitOfWork, ETOMS_API.Repositories.Impl.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IProfileService, ETOMS_API.Services.Impl.ProfileService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IOrganizationService, ETOMS_API.Services.Impl.OrganizationService>();
builder.Services.AddScoped<ITournamentRegistrationService, TournamentRegistrationService>();
builder.Services.AddSingleton<ETOMS_API.Utils.PrivateFileStorage>();
builder.Services.AddScoped<ICurrentUser, CurrentUserStub>();

// Service của từng module đăng ký ở đây.
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IBracketService, BracketService>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IMatchOpsService, ETOMS_API.Services.Impl.MatchOpsService>();
builder.Services.AddHostedService<WaitlistExpirationWorker>();

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
