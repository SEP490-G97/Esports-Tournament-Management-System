using ETOMS.Application._Example;
using Microsoft.Extensions.DependencyInjection;

namespace ETOMS.Application;

public static class DependencyInjection
{
    // Goi tu Program.cs: builder.Services.AddApplication();
    // Moi khi tao Service moi, dang ky o day:
    //   services.AddScoped<ITeamService, TeamService>();
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISampleService, SampleService>();
        return services;
    }
}
