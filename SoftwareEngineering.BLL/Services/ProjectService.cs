using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;
using static SoftwareEngineering.BLL.Models.ProjectModel;

namespace SoftwareEngineering.BLL.Services;

public class ProjectService : BaseService, IProjectService
{
    private const int MaxNameLength = 150;

    private readonly IProjectRepository projectRepository;

    public ProjectService(IProjectRepository projectRepository)
    {
        this.projectRepository = projectRepository;
    }

    public async Task<IEnumerable<ProjectModel>> GetUserProjectsAsync(Guid userId)
    {
        return await this.ExecuteWithLoggingAsync("Отримання проєктів користувача", async () =>
        {
            var projects = await this.projectRepository.GetAllByUserIdAsync(userId);
            return projects.Select(ToModel);
        });
    }

    public async Task<ProjectModel?> GetByIdAsync(Guid id)
    {
        return await this.ExecuteWithLoggingAsync("Отримання проєкту за ID", async () =>
        {
            var project = await this.projectRepository.GetByIdAsync(id);
            return project != null ? ToModel(project) : null;
        });
    }

    public async Task<ProjectModel> CreateProjectAsync(CreateProjectModel model, Guid currentUserId)
    {
        return await this.ExecuteWithLoggingAsync("Створення проєкту", async () =>
        {
            this.ValidateNotEmpty(model.Name, "Назва проєкту");
            var trimmedName = model.Name.Trim();
            this.ValidateCondition(trimmedName.Length <= MaxNameLength, "Назва проєкту занадто довга.");

            var existing = await this.projectRepository.FindByNameAsync(trimmedName);
            this.ValidateCondition(existing == null, "Проєкт з такою назвою вже існує.");

            var ownerId = model.OwnerId ?? currentUserId;
            if (ownerId == Guid.Empty)
            {
                var defaultOwner = await this.projectRepository.GetDefaultOwnerIdAsync();
                this.ValidateCondition(defaultOwner.HasValue, "У системі немає жодного користувача для призначення власником проєкту.");
                ownerId = defaultOwner!.Value;
            }

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                OwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            };

            await this.projectRepository.AddAsync(project);
            this.Logger.Information("Створено новий проєкт {ProjectName} ({ProjectId})", project.Name, project.Id);

            return ToModel(project);
        });
    }

    public async Task UpdateProjectAsync(UpdateProjectModel model)
    {
        await this.ExecuteWithLoggingAsync("Оновлення проєкту", async () =>
        {
            this.ValidateNotEmpty(model.Name, "Назва проєкту");
            var trimmedName = model.Name.Trim();
            this.ValidateCondition(trimmedName.Length <= MaxNameLength, "Назва проєкту занадто довга.");

            var project = await this.projectRepository.GetByIdAsync(model.Id);
            this.ValidateCondition(project != null, "Проєкт не знайдено.");

            project!.Name = trimmedName;
            await this.projectRepository.UpdateAsync(project);

            this.Logger.Information("Оновлено проєкт {ProjectId}: нова назва {ProjectName}", model.Id, trimmedName);
            return true;
        });
    }

    public async Task DeleteProjectAsync(Guid id)
    {
        await this.ExecuteWithLoggingAsync("Видалення проєкту", async () =>
        {
            var project = await this.projectRepository.GetByIdAsync(id);
            this.ValidateCondition(project != null, "Проєкт не знайдено.");

            await this.projectRepository.DeleteAsync(id);
            this.Logger.Information("Видалено проєкт {ProjectId}", id);
            return true;
        });
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