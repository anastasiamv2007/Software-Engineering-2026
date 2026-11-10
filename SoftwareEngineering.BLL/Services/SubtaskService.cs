using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class SubtaskService : BaseService, ISubtaskService
{
    public const int MaxTitleLength = 200;

    private const string UnexpectedErrorMessage = "Сталася внутрішня помилка. Спробуйте ще раз.";

    private readonly ISubtaskRepository subtaskRepository;
    private readonly ITaskRepository taskRepository;
    private readonly IProjectRepository projectRepository;

    public SubtaskService(ISubtaskRepository subtaskRepository, ITaskRepository taskRepository, IProjectRepository projectRepository)
    {
        this.subtaskRepository = subtaskRepository;
        this.taskRepository = taskRepository;
        this.projectRepository = projectRepository;
    }

    public async Task<Result<IReadOnlyList<SubtaskModel>>> GetByTaskAsync(Guid taskId, Guid currentUserId)
    {
        try
        {
            var access = await this.GetAccessAsync(taskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail<IReadOnlyList<SubtaskModel>>(error.Type, error.Message);
            }

            if (!access.CanView)
            {
                return this.Fail<IReadOnlyList<SubtaskModel>>(ErrorType.Forbidden, "Переглядати підзадачі можуть лише учасники проєкту.");
            }

            var subtasks = await this.subtaskRepository.GetByTaskIdAsync(taskId);
            IReadOnlyList<SubtaskModel> models = subtasks.Select(ToModel).ToList();
            return Result.Ok(models);
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час отримання підзадач завдання {TaskId}", taskId);
            return Result.Fail<IReadOnlyList<SubtaskModel>>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result<SubtaskModel>> AddAsync(Guid taskId, string title, Guid currentUserId)
    {
        try
        {
            this.Logger.Information("Спроба додати підзадачу до завдання {TaskId} користувачем {UserId}", taskId, currentUserId);

            var trimmedTitle = title?.Trim() ?? string.Empty;
            if (trimmedTitle.Length == 0)
            {
                return this.Fail<SubtaskModel>(ErrorType.Validation, "Назва підзадачі не може бути порожньою.");
            }

            if (trimmedTitle.Length > MaxTitleLength)
            {
                return this.Fail<SubtaskModel>(ErrorType.Validation, $"Назва підзадачі не повинна перевищувати {MaxTitleLength} символів.");
            }

            var access = await this.GetAccessAsync(taskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail<SubtaskModel>(error.Type, error.Message);
            }

            if (!access.CanEdit)
            {
                return this.Fail<SubtaskModel>(ErrorType.Forbidden, "Додавати підзадачі можуть лише учасники проєкту та виконавець завдання.");
            }

            var subtask = new Subtask
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                Title = trimmedTitle,
                IsCompleted = false,
            };

            await this.subtaskRepository.AddAsync(subtask);
            this.Logger.Information("Створено підзадачу {SubtaskId} для завдання {TaskId}", subtask.Id, taskId);

            return Result.Ok(ToModel(subtask));
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час створення підзадачі для завдання {TaskId}", taskId);
            return Result.Fail<SubtaskModel>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result<SubtaskModel>> SetCompletedAsync(Guid subtaskId, bool isCompleted, Guid currentUserId)
    {
        try
        {
            var subtask = await this.subtaskRepository.GetByIdAsync(subtaskId);
            if (subtask is null)
            {
                return this.Fail<SubtaskModel>(ErrorType.NotFound, "Підзадачу не знайдено. Можливо, її вже видалили.");
            }

            var access = await this.GetAccessAsync(subtask.TaskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail<SubtaskModel>(error.Type, error.Message);
            }

            if (!access.CanEdit)
            {
                return this.Fail<SubtaskModel>(ErrorType.Forbidden, "Відмічати підзадачі можуть лише учасники проєкту та виконавець завдання.");
            }

            if (subtask.IsCompleted == isCompleted)
            {
                return Result.Ok(ToModel(subtask));
            }

            subtask.IsCompleted = isCompleted;
            await this.subtaskRepository.UpdateAsync(subtask);
            this.Logger.Information(
                "Користувач {UserId} змінив стан підзадачі {SubtaskId} на {IsCompleted}",
                currentUserId,
                subtaskId,
                isCompleted);

            return Result.Ok(ToModel(subtask));
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час зміни стану підзадачі {SubtaskId}", subtaskId);
            return Result.Fail<SubtaskModel>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    public async Task<Result> DeleteAsync(Guid subtaskId, Guid currentUserId)
    {
        try
        {
            var subtask = await this.subtaskRepository.GetByIdAsync(subtaskId);
            if (subtask is null)
            {
                return this.Fail(ErrorType.NotFound, "Підзадачу не знайдено. Можливо, її вже видалили.");
            }

            var access = await this.GetAccessAsync(subtask.TaskId, currentUserId);
            if (access.Error is { } error)
            {
                return this.Fail(error.Type, error.Message);
            }

            if (!access.CanManage)
            {
                return this.Fail(ErrorType.Forbidden, "Видаляти підзадачі може лише автор завдання або власник проєкту.");
            }

            await this.subtaskRepository.DeleteAsync(subtaskId);
            this.Logger.Information("Користувач {UserId} видалив підзадачу {SubtaskId}", currentUserId, subtaskId);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час видалення підзадачі {SubtaskId}", subtaskId);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    private static SubtaskModel ToModel(Subtask subtask)
    {
        return new SubtaskModel
        {
            Id = subtask.Id,
            TaskId = subtask.TaskId,
            Title = subtask.Title,
            IsCompleted = subtask.IsCompleted,
        };
    }

    /// <summary>
    /// Права користувача щодо підзадач завдання — ті самі правила, що й для самих завдань у TaskService.
    /// </summary>
    private async Task<TaskAccess> GetAccessAsync(Guid taskId, Guid userId)
    {
        if (taskId == Guid.Empty || userId == Guid.Empty)
        {
            return TaskAccess.Failed(ErrorType.Validation, "Не вказано завдання або користувача.");
        }

        var task = await this.taskRepository.GetByIdAsync(taskId);
        if (task is null)
        {
            return TaskAccess.Failed(ErrorType.NotFound, "Завдання не знайдено або його вже видалено.");
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, userId);
        var isTaskOwner = task.OwnerId == userId;
        var isAssignee = task.AssigneeId == userId;

        return new TaskAccess(
            CanView: role != null || isTaskOwner || isAssignee,
            CanEdit: role is ProjectRole.Owner or ProjectRole.CoExecutor || isTaskOwner || isAssignee,
            CanManage: role == ProjectRole.Owner || isTaskOwner,
            Error: null);
    }

    private Result<T> Fail<T>(ErrorType type, string message)
    {
        this.Logger.Warning("Дію з підзадачею відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail<T>(type, message);
    }

    private Result Fail(ErrorType type, string message)
    {
        this.Logger.Warning("Дію з підзадачею відхилено ({ErrorType}): {ErrorMessage}", type, message);
        return Result.Fail(type, message);
    }

    private sealed record TaskAccess(bool CanView, bool CanEdit, bool CanManage, Error? Error)
    {
        public static TaskAccess Failed(ErrorType type, string message) => new(false, false, false, new Error(type, message));
    }
}
