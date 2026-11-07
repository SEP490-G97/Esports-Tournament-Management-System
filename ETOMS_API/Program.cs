using ETOMS_API.Models;
using ETOMS_API.Repositories;
using ETOMS_API.Services.Impl;
using ETOMS_API.Services.Interface;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EtomsDatabase")
    ?? builder.Configuration.GetConnectionString("EtomsDb")
    ?? builder.Configuration["ETOMS_DB_CONNECTION"];
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình kết nối MySQL. Hãy đặt ConnectionStrings:EtomsDatabase hoặc biến môi trường ETOMS_DB_CONNECTION.");
}

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

builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<ETOMS_API.Repositories.IUnitOfWork, ETOMS_API.Repositories.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Repositories.Interface.IUnitOfWork, ETOMS_API.Repositories.Impl.UnitOfWork>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IProfileService, ETOMS_API.Services.Impl.ProfileService>();
builder.Services.AddScoped<ETOMS_API.Services.Interface.IOrganizationService, ETOMS_API.Services.Impl.OrganizationService>();
builder.Services.AddSingleton<ETOMS_API.Utils.PrivateFileStorage>();
builder.Services.AddScoped<ICurrentUser, CurrentUserStub>();
builder.Services.AddScoped<ITeamService, TeamService>();

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
app.UseAuthorization();

app.MapControllers();

app.Run();
