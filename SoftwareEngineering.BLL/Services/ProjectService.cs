using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
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

    public Task<Result<IReadOnlyList<ProjectModel>>> GetUserProjectsAsync(Guid userId)
    {
        return this.ExecuteAsync("Отримання проєктів користувача", async () =>
        {
            if (userId == Guid.Empty)
            {
                return Result.Fail<IReadOnlyList<ProjectModel>>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
            }

            var projects = await this.projectRepository.GetAllByUserIdAsync(userId);
            IReadOnlyList<ProjectModel> models = projects.Select(ToModel).ToList();
            return Result.Ok(models);
        });
    }

    public Task<Result<ProjectModel>> GetByIdAsync(Guid id)
    {
        return this.ExecuteAsync("Отримання проєкту за ID", async () =>
        {
            var project = await this.projectRepository.GetByIdAsync(id);
            return project is null
                ? Result.Fail<ProjectModel>(ErrorType.NotFound, "Проєкт не знайдено.")
                : Result.Ok(ToModel(project));
        });
    }

    public Task<Result<ProjectModel>> CreateProjectAsync(CreateProjectModel model, Guid currentUserId)
    {
        return this.ExecuteAsync("Створення проєкту", async () =>
        {
            if (model is null)
            {
                return Result.Fail<ProjectModel>(ErrorType.Validation, "Дані проєкту не передано.");
            }

            var nameError = ValidateName(model.Name);
            if (nameError is not null)
            {
                return Result.Fail<ProjectModel>(ErrorType.Validation, nameError);
            }

            var trimmedName = model.Name.Trim();
            var ownerId = model.OwnerId ?? currentUserId;
            if (ownerId == Guid.Empty)
            {
                return Result.Fail<ProjectModel>(ErrorType.Validation, "Не вказано власника проєкту.");
            }

            var existing = await this.projectRepository.FindByNameAsync(trimmedName);
            if (existing is not null)
            {
                return Result.Fail<ProjectModel>(ErrorType.Duplicate, "Проєкт з такою назвою вже існує.");
            }

            var project = await this.AddProjectAsync(trimmedName, ownerId);
            return Result.Ok(ToModel(project));
        });
    }

    public Task<Result> UpdateProjectAsync(UpdateProjectModel model, Guid currentUserId)
    {
        return this.ExecuteAsync("Оновлення проєкту", async () =>
        {
            if (model is null)
            {
                return Result.Fail(ErrorType.Validation, "Дані проєкту не передано.");
            }

            var nameError = ValidateName(model.Name);
            if (nameError is not null)
            {
                return Result.Fail(ErrorType.Validation, nameError);
            }

            var project = await this.projectRepository.GetByIdAsync(model.Id);
            if (project is null)
            {
                return Result.Fail(ErrorType.NotFound, "Проєкт не знайдено.");
            }

            if (project.OwnerId != currentUserId)
            {
                return Result.Fail(ErrorType.Forbidden, "Редагувати проєкт може лише його власник.");
            }

            var trimmedName = model.Name.Trim();

            // Інший проєкт з такою назвою — дубль (свою ж назву можна лишити без змін)
            var sameName = await this.projectRepository.FindByNameAsync(trimmedName);
            if (sameName is not null && sameName.Id != project.Id)
            {
                return Result.Fail(ErrorType.Duplicate, "Проєкт з такою назвою вже існує.");
            }

            project.Name = trimmedName;
            await this.projectRepository.UpdateAsync(project);

            this.Logger.Information("Оновлено проєкт {ProjectId}: нова назва {ProjectName}", model.Id, trimmedName);
            return Result.Ok();
        });
    }

    public Task<Result> DeleteProjectAsync(Guid id, Guid currentUserId)
    {
        return this.ExecuteAsync("Видалення проєкту", async () =>
        {
            var project = await this.projectRepository.GetByIdAsync(id);
            if (project is null)
            {
                return Result.Fail(ErrorType.NotFound, "Проєкт не знайдено.");
            }

            // Видалення каскадно прибирає всі завдання проєкту, тому дозволено лише власнику
            if (project.OwnerId != currentUserId)
            {
                return Result.Fail(ErrorType.Forbidden, "Видалити проєкт може лише його власник.");
            }

            await this.projectRepository.DeleteAsync(id);
            this.Logger.Information("Користувач {UserId} видалив проєкт {ProjectId}", currentUserId, id);
            return Result.Ok();
        });
    }

    public Task<Result<ProjectModel>> GetOrCreateByNameAsync(string name, Guid ownerId)
    {
        return this.ExecuteAsync("Отримання або створення проєкту", async () =>
        {
            var nameError = ValidateName(name);
            if (nameError is not null)
            {
                return Result.Fail<ProjectModel>(ErrorType.Validation, nameError);
            }

            if (ownerId == Guid.Empty)
            {
                return Result.Fail<ProjectModel>(ErrorType.Validation, "Не вказано власника проєкту.");
            }

            var trimmedName = name.Trim();
            var existing = await this.projectRepository.FindByNameAsync(trimmedName);
            if (existing is not null)
            {
                return Result.Ok(ToModel(existing));
            }

            var project = await this.AddProjectAsync(trimmedName, ownerId);
            return Result.Ok(ToModel(project));
        });
    }

    private static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Назва проєкту не може бути порожньою.";
        }

        return name.Trim().Length > MaxNameLength ? "Назва проєкту занадто довга." : null;
    }

    private async Task<Project> AddProjectAsync(string name, Guid ownerId)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };

        // Автор проєкту одразу стає його учасником з роллю Owner
        project.Members.Add(new ProjectMember { UserId = ownerId, Role = ProjectRole.Owner });

        await this.projectRepository.AddAsync(project);
        this.Logger.Information("Створено проєкт {ProjectName} ({ProjectId}) власником {UserId}", project.Name, project.Id, ownerId);

        return project;
    }

    private static ProjectModel ToModel(Project project)
    {
        return new ProjectModel
        {
            Id = project.Id,
            Name = project.Name,
            OwnerId = project.OwnerId,
            CreatedAt = project.CreatedAt,
            TasksCount = project.Tasks.Count,
        };
    }
}
