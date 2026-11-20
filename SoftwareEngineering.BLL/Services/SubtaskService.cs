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

    private readonly ISubtaskRepository subtaskRepository;
    private readonly ITaskRepository taskRepository;
    private readonly IProjectRepository projectRepository;

    public SubtaskService(ISubtaskRepository subtaskRepository, ITaskRepository taskRepository, IProjectRepository projectRepository)
    {
        this.subtaskRepository = subtaskRepository;
        this.taskRepository = taskRepository;
        this.projectRepository = projectRepository;
    }

    public Task<Result<IReadOnlyList<SubtaskModel>>> GetByTaskAsync(Guid taskId, Guid currentUserId)
    {
        return this.ExecuteAsync("Отримання підзадач", async () =>
        {
            var access = await this.GetAccessAsync(taskId, currentUserId);
            if (access.Error is { } error)
            {
                return Result.Fail<IReadOnlyList<SubtaskModel>>(error.Type, error.Message);
            }

            if (!access.CanView)
            {
                return Result.Fail<IReadOnlyList<SubtaskModel>>(ErrorType.Forbidden, "Переглядати підзадачі можуть лише учасники проєкту.");
            }

            var subtasks = await this.subtaskRepository.GetByTaskIdAsync(taskId);
            IReadOnlyList<SubtaskModel> models = subtasks.Select(ToModel).ToList();
            return Result.Ok(models);
        });
    }

    public Task<Result<SubtaskModel>> AddAsync(Guid taskId, string title, Guid currentUserId)
    {
        return this.ExecuteAsync("Створення підзадачі", async () =>
        {
            var trimmedTitle = title?.Trim() ?? string.Empty;
            if (trimmedTitle.Length == 0)
            {
                return Result.Fail<SubtaskModel>(ErrorType.Validation, "Назва підзадачі не може бути порожньою.");
            }

            if (trimmedTitle.Length > MaxTitleLength)
            {
                return Result.Fail<SubtaskModel>(ErrorType.Validation, $"Назва підзадачі не повинна перевищувати {MaxTitleLength} символів.");
            }

            var access = await this.GetAccessAsync(taskId, currentUserId);
            if (access.Error is { } error)
            {
                return Result.Fail<SubtaskModel>(error.Type, error.Message);
            }

            if (!access.CanEdit)
            {
                return Result.Fail<SubtaskModel>(ErrorType.Forbidden, "Додавати підзадачі можуть лише учасники проєкту та виконавець завдання.");
            }

            var subtask = new Subtask
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                Title = trimmedTitle,
                IsCompleted = false,
            };

            await this.subtaskRepository.AddAsync(subtask);
            this.Logger.Information("Користувач {UserId} створив підзадачу {SubtaskId} для завдання {TaskId}", currentUserId, subtask.Id, taskId);

            return Result.Ok(ToModel(subtask));
        });
    }

    public Task<Result<SubtaskModel>> SetCompletedAsync(Guid subtaskId, bool isCompleted, Guid currentUserId)
    {
        return this.ExecuteAsync("Зміна стану підзадачі", async () =>
        {
            var subtask = await this.subtaskRepository.GetByIdAsync(subtaskId);
            if (subtask is null)
            {
                return Result.Fail<SubtaskModel>(ErrorType.NotFound, "Підзадачу не знайдено. Можливо, її вже видалили.");
            }

            var access = await this.GetAccessAsync(subtask.TaskId, currentUserId);
            if (access.Error is { } error)
            {
                return Result.Fail<SubtaskModel>(error.Type, error.Message);
            }

            if (!access.CanEdit)
            {
                return Result.Fail<SubtaskModel>(ErrorType.Forbidden, "Відмічати підзадачі можуть лише учасники проєкту та виконавець завдання.");
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
        });
    }

    public Task<Result> DeleteAsync(Guid subtaskId, Guid currentUserId)
    {
        return this.ExecuteAsync("Видалення підзадачі", async () =>
        {
            var subtask = await this.subtaskRepository.GetByIdAsync(subtaskId);
            if (subtask is null)
            {
                return Result.Fail(ErrorType.NotFound, "Підзадачу не знайдено. Можливо, її вже видалили.");
            }

            var access = await this.GetAccessAsync(subtask.TaskId, currentUserId);
            if (access.Error is { } error)
            {
                return Result.Fail(error.Type, error.Message);
            }

            if (!access.CanManage)
            {
                return Result.Fail(ErrorType.Forbidden, "Видаляти підзадачі може лише автор завдання або власник проєкту.");
            }

            await this.subtaskRepository.DeleteAsync(subtaskId);
            this.Logger.Information("Користувач {UserId} видалив підзадачу {SubtaskId}", currentUserId, subtaskId);

            return Result.Ok();
        });
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

    private sealed record TaskAccess(bool CanView, bool CanEdit, bool CanManage, Error? Error)
    {
        public static TaskAccess Failed(ErrorType type, string message) => new(false, false, false, new Error(type, message));
    }
}
