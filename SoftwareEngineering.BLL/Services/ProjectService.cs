using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class ProjectService : BaseService, IProjectService
{
    private const int MaxNameLength = 150;

    private readonly IProjectRepository projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        this.projectRepository = projectRepository;
    }

    public async Task<ProjectModel> GetOrCreateByNameAsync(string name)
    {
        return await this.ExecuteWithLoggingAsync("Отримання або створення проєкту", async () =>
        {
            this.ValidateNotEmpty(name, "Проєкт");
            var trimmedName = name.Trim();
            this.ValidateCondition(trimmedName.Length <= MaxNameLength, "Назва проєкту занадто довга.");

            var existing = await this.projectRepository.FindByNameAsync(trimmedName);
            if (existing != null)
            {
                return ToModel(existing);
            }

            var ownerId = await this.projectRepository.GetDefaultOwnerIdAsync();
            this.ValidateCondition(ownerId.HasValue, "У системі немає жодного користувача для призначення власником проєкту.");

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                OwnerId = ownerId!.Value,
            };

            await this.projectRepository.AddAsync(project);
            this.Logger.Information("Створено проєкт {ProjectName} ({ProjectId})", project.Name, project.Id);

            return ToModel(project);
        });
    }

    private static ProjectModel ToModel(Project project)
    {
        return new ProjectModel { Id = project.Id, Name = project.Name };
    }
}