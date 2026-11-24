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

    public async Task<Result<IReadOnlyList<ProjectModel>>> GetUserProjectsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return this.Fail<IReadOnlyList<ProjectModel>>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
        }

        var projects = await this.projectRepository.GetAllByUserIdAsync(userId);
        IReadOnlyList<ProjectModel> models = projects.Select(ToModel).ToList();
        return Result.Ok(models);
    }

    public async Task<Result<ProjectModel>> GetByIdAsync(Guid id)
    {
        var project = await this.projectRepository.GetByIdAsync(id);
        return project is null
            ? this.Fail<ProjectModel>(ErrorType.NotFound, "Проєкт не знайдено.")
            : Result.Ok(ToModel(project));
    }

    public async Task<Result<ProjectModel>> CreateProjectAsync(CreateProjectModel model, Guid currentUserId)
    {
        if (model is null)
        {
            return this.Fail<ProjectModel>(ErrorType.Validation, "Дані проєкту не передано.");
        }

        var nameError = ValidateName(model.Name);
        if (nameError is not null)
        {
            return this.Fail<ProjectModel>(ErrorType.Validation, nameError);
        }

        var trimmedName = model.Name.Trim();
        var ownerId = model.OwnerId ?? currentUserId;
        if (ownerId == Guid.Empty)
        {
            return this.Fail<ProjectModel>(ErrorType.Validation, "Не вказано власника проєкту.");
        }

        var existing = await this.projectRepository.FindByNameAsync(trimmedName);
        if (existing is not null)
        {
            return this.Fail<ProjectModel>(ErrorType.Duplicate, "Проєкт з такою назвою вже існує.");
        }

        var project = await this.AddProjectAsync(trimmedName, ownerId);
        return Result.Ok(ToModel(project));
    }

    public async Task<Result> UpdateProjectAsync(UpdateProjectModel model, Guid currentUserId)
    {
        if (model is null)
        {
            return this.Fail(ErrorType.Validation, "Дані проєкту не передано.");
        }

        var nameError = ValidateName(model.Name);
        if (nameError is not null)
        {
            return this.Fail(ErrorType.Validation, nameError);
        }

        var project = await this.projectRepository.GetByIdAsync(model.Id);
        if (project is null)
        {
            return this.Fail(ErrorType.NotFound, "Проєкт не знайдено.");
        }

        if (project.OwnerId != currentUserId)
        {
            return this.Fail(ErrorType.Forbidden, "Редагувати проєкт може лише його власник.");
        }

        var trimmedName = model.Name.Trim();

        // Інший проєкт з такою назвою — дубль (свою ж назву можна лишити без змін)
        var sameName = await this.projectRepository.FindByNameAsync(trimmedName);
        if (sameName is not null && sameName.Id != project.Id)
        {
            return this.Fail(ErrorType.Duplicate, "Проєкт з такою назвою вже існує.");
        }

        project.Name = trimmedName;
        await this.projectRepository.UpdateAsync(project);

        this.Logger.Information("Оновлено проєкт {ProjectId}: нова назва {ProjectName}", model.Id, trimmedName);
        return Result.Ok();
    }

    public async Task<Result> DeleteProjectAsync(Guid id, Guid currentUserId)
    {
        var project = await this.projectRepository.GetByIdAsync(id);
        if (project is null)
        {
            return this.Fail(ErrorType.NotFound, "Проєкт не знайдено.");
        }

        // Видалення каскадно прибирає всі завдання проєкту, тому дозволено лише власнику
        if (project.OwnerId != currentUserId)
        {
            return this.Fail(ErrorType.Forbidden, "Видалити проєкт може лише його власник.");
        }

        await this.projectRepository.DeleteAsync(id);
        this.Logger.Information("Користувач {UserId} видалив проєкт {ProjectId}", currentUserId, id);
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<ProjectModel>>> GetProjectsForNewTaskAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return this.Fail<IReadOnlyList<ProjectModel>>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
        }

        var projects = await this.projectRepository.GetAllByUserIdAsync(userId);
        var allowed = new List<ProjectModel>();
        foreach (var project in projects)
        {
            // Те саме правило, що перевіряє TaskService.CreateAsync, тож у списку немає проєктів, де створення відхилять
            var role = await this.projectRepository.GetMemberRoleAsync(project.Id, userId);
            if (ProjectPermissions.CanCreateTasks(role))
            {
                allowed.Add(ToModel(project));
            }
        }

        IReadOnlyList<ProjectModel> ordered = allowed.OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return Result.Ok(ordered);
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
