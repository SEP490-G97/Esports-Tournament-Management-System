using ETOMS.Application.Interfaces;
using ETOMS.Application.Services;
using ETOMS.Infrastructure.Data;
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

// EF Core — SQL Server. Chuỗi kết nối để trong appsettings.Development.json (KHÔNG commit).
// Repository + UnitOfWork chỉ đăng ký khi đã cấu hình database (sau khi scaffold - CLAUDE.md mục 6).
var connectionString = builder.Configuration.GetConnectionString("EtomsDb");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<EtomsDbContext>(options =>
        options.UseSqlServer(connectionString));

    builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
}

// Đăng ký Service của từng module ở đây (vd: AddScoped<ITeamService, TeamService>()).
builder.Services.AddScoped<ISampleService, SampleService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

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
