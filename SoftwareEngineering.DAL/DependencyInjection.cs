using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        // DbContext не потокобезпечний: у WPF кілька вікон/вкладок вантажать дані одночасно,
        // тому кожна операція репозиторію створює власний короткоживучий контекст через фабрику
        services.AddDbContextFactory<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IProjectRepository, ProjectRepository>();

        return services;
    }
}