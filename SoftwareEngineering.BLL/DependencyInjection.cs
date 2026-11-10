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
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IDatabaseSetupService, DatabaseSetupService>();
        services.AddSingleton<IUserSession, UserSession>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ISubtaskService, SubtaskService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProjectMemberService, ProjectMemberService>();
        return services;
    }
}