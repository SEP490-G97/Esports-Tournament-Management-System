using ETOMS.Application.Interfaces;
using ETOMS.Infrastructure.Data;
using ETOMS.Infrastructure.Identity;
using ETOMS.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC (Controllers + Views) + API Controllers trong cùng 1 project.
builder.Services.AddControllersWithViews();

// SignalR cho thông báo/tỉ số trực tiếp (NOT-01, LIVE).
builder.Services.AddSignalR();

// Swagger UI để test API trực tiếp trên trình duyệt: /swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AutoMapper: quét toàn bộ Profile trong assembly (Application/Mappings/*).
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());

// Session (phục vụ stub đăng nhập demo — thay khi có AUTH thật).
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// EF Core — SQL Server. Chuỗi kết nối trong appsettings.Development.json (KHÔNG commit).
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

// Service của từng module đăng ký ở đây (ví dụ: AddScoped<ITeamService, TeamService>()).

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Trang lỗi tùy biến (KHÔNG dùng trang mặc định của trình duyệt):
//  - UseExceptionHandler: lỗi 500 chưa bắt được → render trang /Loi/500
//  - UseStatusCodePagesWithReExecute: các mã lỗi (404, 403...) → render /Loi/<mã>
app.UseExceptionHandler("/Loi/500");
app.UseStatusCodePagesWithReExecute("/Loi/{0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// API controllers (attribute routing [Route("api/...")]).
app.MapControllers();

// app.MapHub<...>("/hubs/...");  // đăng ký SignalR Hub cụ thể khi tạo Hub

app.Run();
