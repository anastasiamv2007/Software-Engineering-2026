using Microsoft.Extensions.DependencyInjection;
using SoftwareEngineering.DAL;

namespace SoftwareEngineering.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, string connectionString)
    {
        services.AddDataAccess(connectionString);

        // Реєстрація сервісів BLL для Use-Cases (приклад: services.AddScoped<IMyService, MyService>();)
        return services;
    }
}