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





// Email: Enabled=false thì chỉ ghi link ra log, test gửi mail trong env dev
var emailEnabled = builder.Configuration.GetValue<bool>("Email:Enabled");
if (emailEnabled) builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
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

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddDbContext<EtomsDbContext>(options => options.UseMySQL(mysqlConnectionString));

builder.Services.AddScoped<IAdminAccountService, AdminAccountService>();
builder.Services.AddScoped<IOneTimeTokenService, OneTimeTokenService>();
builder.Services.AddScoped<IAccountSecurityService, AccountSecurityService>();
builder.Services.AddScoped(typeof(ETOMS_API.Repositories.IGenericRepository<>), typeof(ETOMS_API.Repositories.GenericRepository<>));
builder.Services.AddScoped<ETOMS_API.Repositories.IUnitOfWork, ETOMS_API.Repositories.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Repositories.Interface.IUnitOfWork, ETOMS_API.Repositories.Impl.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IProfileService, ETOMS_API.Services.Impl.ProfileService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IOrganizationService, ETOMS_API.Services.Impl.OrganizationService>();
builder.Services.AddSingleton<ETOMS_API.Utils.PrivateFileStorage>();
builder.Services.AddScoped<ICurrentUser, CurrentUserStub>();
builder.Services.AddScoped<ITeamService, TeamService>();

builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountBanService, AccountBanService>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

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
app.MapControllers();

app.Run();
