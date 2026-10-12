using Microsoft.Extensions.DependencyInjection;
using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL;

namespace SoftwareEngineering.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, string connectionString)
    {
        services.AddDataAccess(connectionString);

        services.AddScoped<ITaskService, TaskService>();

        // Реєстрація сервісів BLL для Use-Cases (приклад: services.AddScoped<IMyService, MyService>();)
        return services;
    }
}