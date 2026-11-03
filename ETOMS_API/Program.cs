using ETOMS_API.Repositories;
using ETOMS_API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Chỉ Web API (JSON) — không còn Razor View engine. Giao diện là HTML/CSS/JS thuần trong
// wwwroot/, gọi các Controller này qua fetch().
builder.Services.AddControllers();

// SignalR cho thông báo/tỉ số trực tiếp (NOT-01, LIVE).
builder.Services.AddSignalR();

// Swagger UI để test API trực tiếp trên trình duyệt: /swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AutoMapper: quét toàn bộ Profile trong assembly.
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

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

// EF Core — SQL Server. Chuỗi kết nối trong appsettings.Development.json (KHÔNG commit).
// Đúng theo Report 4 (Software Design Specification): Controller → Service → IUnitOfWork
// (phát GenericRepository<T> theo entity, dùng chung 1 EtomsDbContext/request) → DB.
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

// Service của từng module đăng ký ở đây.
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IBracketService, BracketService>();
builder.Services.AddScoped<IResultService, ResultService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Phục vụ file tĩnh trong wwwroot (html/css/js) — "/" tự tìm wwwroot/index.html.
app.UseDefaultFiles();
app.UseStaticFiles();

// Trang lỗi tĩnh (wwwroot/errors/*.html, KHÔNG dùng trang mặc định của trình duyệt):
//  - UseExceptionHandler: lỗi 500 chưa bắt được → render /errors/500.html
//  - UseStatusCodePagesWithReExecute: các mã lỗi (404, 403...) → render /errors/<mã>.html
// Loại trừ /api/*: API phải luôn trả JSON thật (kể cả lỗi), không bị ghi đè thành trang HTML.
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), branch =>
{
    branch.UseExceptionHandler("/errors/500.html");
    branch.UseStatusCodePagesWithReExecute("/errors/{0}.html");
});

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

// API controllers (attribute routing [Route("api/...")]) — không còn route quy ước MVC.
app.MapControllers();

// app.MapHub<...>("/hubs/...");  // đăng ký SignalR Hub cụ thể khi tạo Hub

app.Run();
