using ETOMS_API.Repositories;
using ETOMS_API.Services;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Chỉ Web API (JSON) — giao diện là HTML/CSS/JS thuần trong wwwroot/, gọi Controller qua fetch().
builder.Services.AddControllers();

// Swagger UI để test API trực tiếp trên trình duyệt: /swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Session (phục vụ stub đăng nhập demo — thay khi có AUTH thật).
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // API controllers xác thực qua cookie session này nhưng KHÔNG có antiforgery token
    // như form MVC → bắt buộc Strict để trình duyệt không gửi cookie kèm request
    // POST/DELETE bắt nguồn từ site khác (chống CSRF).
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// EF Core — SQL Server. Chuỗi kết nối trong appsettings.Development.json (KHÔNG commit).
// Controller → Service → IUnitOfWork (phát GenericRepository<T> theo entity, dùng chung
// 1 EtomsDbContext/request) → DB.
var connectionString = builder.Configuration.GetConnectionString("EtomsDb");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<EtomsDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
}

// Người dùng hiện tại (stub tạm — đọc từ session; thay bằng JWT/Claims khi có AUTH).
builder.Services.AddScoped<ICurrentUser, CurrentUserStub>();

// Service của từng module đăng ký ở đây — thêm dòng khi module đó có code.
builder.Services.AddScoped<ITeamService, TeamService>();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Trang lỗi tĩnh (wwwroot/errors/*.html). PHẢI đứng TRƯỚC UseStaticFiles — re-execute chỉ
// đi tiếp qua middleware đăng ký SAU nó, nên static-file middleware phải nằm sau để bị
// re-execute vào được. Loại trừ /api/*: API phải luôn trả JSON thật (kể cả lỗi), không
// bị ghi đè thành trang HTML.
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseExceptionHandler("/errors/500.html");
    branch.UseStatusCodePagesWithReExecute("/errors/{0}.html");
});

// Phục vụ file tĩnh trong wwwroot (html/css/js) — "/" tự tìm wwwroot/index.html.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllers();

app.Run();
