using ETOMS.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ETOMS.Infrastructure;

public static class DependencyInjection
{
    // Goi tu Program.cs: builder.Services.AddInfrastructure();
    // Luu y: phai AddDbContext<EtomsDbContext>(...) truoc, va dang ky them
    // services.AddScoped<DbContext>(sp => sp.GetRequiredService<EtomsDbContext>());
    // de GenericRepository<T> resolve duoc DbContext (xem CLAUDE.md muc 6 ve lenh scaffold).
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        return services;
    }
}
