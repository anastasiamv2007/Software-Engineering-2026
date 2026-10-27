using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Права в проєкті: Owner може все; співвиконавець створює задачі, а видаляє та змінює дедлайн лише у своїх.
/// </summary>
public class TaskService : BaseService, ITaskService
{
    private const int MaxTitleLength = 200;
    private const string TaskCodePrefix = "PL-";

    private readonly ITaskRepository taskRepository;
    private readonly IProjectRepository projectRepository;

    public TaskService(ITaskRepository taskRepository, IProjectRepository projectRepository)
    {
        this.taskRepository = taskRepository;
        this.projectRepository = projectRepository;
    }

    public async Task<IReadOnlyList<TaskModel>> GetAllAsync()
    {
        return await this.ExecuteWithLoggingAsync("Отримання списку всіх завдань", async () =>
        {
            var entities = await this.taskRepository.GetAsync(); // Виправлено назву методу репозиторію
            return entities.Select(ToModel).ToList().AsReadOnly();
        });
    }

    public async Task<TaskModel> CreateAsync(TaskModel model)
    {
        this.ValidateNewTask(model);

        return await this.ExecuteWithLoggingAsync("Створення завдання", async () =>
        {
            var role = await this.projectRepository.GetMemberRoleAsync(model.ProjectId, model.OwnerId);
            this.ValidateCondition(
                role is ProjectRole.Owner or ProjectRole.CoExecutor,
                "Створювати завдання можуть лише власник проєкту та співвиконавці.");

            var entity = await this.BuildEntityAsync(model);
            await this.taskRepository.AddAsync(entity);

            return ToModel(entity);
        });
    }

    // 2
    public async Task<TaskModel> GetByIdAsync(Guid id)
    {
        return await this.ExecuteWithLoggingAsync("Перегляд деталей завдання", async () =>
        {
            this.ValidateCondition(id != Guid.Empty, "Ідентифікатор завдання не може бути порожнім.");

            var entity = await this.taskRepository.GetByIdAsync(id);

            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");

            return ToModel(entity!);
        });
    }

    // 3
    public async Task<TaskModel> UpdateAsync(TaskModel model, Guid currentUserId)
    {
        return await this.ExecuteWithLoggingAsync("Редагування завдання", async () =>
        {
            this.ValidateUpdateTask(model);
            this.ValidateCondition(currentUserId != Guid.Empty, "Ідентифікатор користувача не вказано.");

            var entity = await this.taskRepository.GetByIdAsync(model.Id);
            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");

            var role = await this.projectRepository.GetMemberRoleAsync(entity!.ProjectId, currentUserId);
            this.ValidateCondition(
                entity.OwnerId == currentUserId || role is ProjectRole.Owner or ProjectRole.CoExecutor,
                "Редагувати завдання можуть лише власник проєкту та співвиконавці.");

            var newDeadline = model.Deadline?.ToUniversalTime();
            if (newDeadline != entity.Deadline)
            {
                this.ValidateCondition(
                    CanManage(entity, currentUserId, role),
                    "Змінювати дедлайн чужого завдання може лише власник проєкту.");
            }

            entity.Title = model.Title.Trim();
            entity.Description = model.Description;
            entity.Deadline = newDeadline;
            entity.Priority = model.Priority;
            entity.CategoryId = model.CategoryId;

            await this.taskRepository.UpdateAsync(entity);

            return ToModel(entity);
        });
    }

    // 4
    public async Task<TaskModel> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId)
    {
        return await this.ExecuteWithLoggingAsync("Видалення завдання", async () =>
        {
            this.ValidateCondition(model != null, "Дані завдання не передано.");
            this.ValidateCondition(model!.Id != Guid.Empty, "Ідентифікатор завдання не вказано.");
            this.ValidateCondition(currentUserId != Guid.Empty, "Ідентифікатор користувача не вказано.");

            var entity = await this.taskRepository.GetByIdAsync(model.Id);
            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");
            this.ValidateCondition(
                await this.CanManageAsync(entity!, currentUserId),
                "Видаляти можна лише власні завдання (власник проєкту може видаляти будь-які).");

            await this.taskRepository.DeleteAsync(entity!);

            return ToModel(entity!);
        });
    }

    // 5
    public async Task DeleteTaskAsync(Guid taskId, Guid currentUserId)
    {
        await this.ExecuteWithLoggingAsync("Видалення спільного завдання", async () =>
        {
            this.ValidateCondition(taskId != Guid.Empty, "Ідентифікатор завдання не вказано.");
            this.ValidateCondition(currentUserId != Guid.Empty, "Ідентифікатор користувача не вказано.");

            var task = await this.taskRepository.GetByIdAsync(taskId);
            this.ValidateCondition(task != null, "Завдання не знайдено або вже було видалено.");
            this.ValidateCondition(
                await this.CanManageAsync(task!, currentUserId),
                "У вас немає прав на видалення. Співвиконавець може вилучати лише власні завдання.");

            await this.taskRepository.DeleteAsync(taskId);
            return true;
        });
    }

    // 6
    public async Task<TaskModel> UpdateDeadlineAsync(Guid taskId, DateTime? newDeadline, Guid currentUserId)
    {
        return await this.ExecuteWithLoggingAsync("Редагування дедлайну спільного завдання", async () =>
        {
            this.ValidateCondition(taskId != Guid.Empty, "Ідентифікатор завдання не вказано.");
            this.ValidateCondition(currentUserId != Guid.Empty, "Ідентифікатор користувача не вказано.");
            this.ValidateCondition(newDeadline.HasValue, "Дедлайн не може бути порожнім.");
            this.ValidateCondition(newDeadline!.Value.ToUniversalTime() > DateTime.UtcNow, "Дедлайн не може бути в минулому.");

            var entity = await this.taskRepository.GetByIdAsync(taskId);
            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");

            // Обмежена дія: дедлайн змінює автор завдання або власник проєкту
            this.ValidateCondition(
                await this.CanManageAsync(entity!, currentUserId),
                "Обмежена дія: лише власник завдання або проєкту має право змінювати дедлайн.");

            var oldDeadline = entity!.Deadline;
            entity.Deadline = newDeadline.Value.ToUniversalTime();

            await this.taskRepository.UpdateAsync(entity);

            this.Logger.Information(
                "Власник {UserId} змінив дедлайн спільного завдання {TaskId} з {OldDeadline} на {NewDeadline}",
                currentUserId,
                taskId,
                oldDeadline,
                entity.Deadline);

            return ToModel(entity);
        });
    }

    // 7
    public async Task<TaskModel> UpdateProgressAsync(Guid taskId, TaskItemStatus newStatus, Guid currentUserId)
    {
        return await this.ExecuteWithLoggingAsync("Оновлення прогресу завдання", async () =>
        {
            this.ValidateCondition(taskId != Guid.Empty, "Ідентифікатор завдання не вказано.");
            this.ValidateCondition(currentUserId != Guid.Empty, "Ідентифікатор користувача не вказано.");
            this.ValidateCondition(Enum.IsDefined(newStatus), "Невідомий статус завдання.");

            var entity = await this.taskRepository.GetByIdAsync(taskId);
            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");
            this.ValidateCondition(
                entity!.AssigneeId == currentUserId,
                "Лише співвиконавець завдання може оновлювати прогрес.");
            this.ValidateCondition(entity.Status != newStatus, "Завдання вже має такий статус.");

            var oldStatus = entity.Status;
            entity.Status = newStatus;

            await this.taskRepository.UpdateAsync(entity);

            this.Logger.Information(
                "Користувач {UserId} змінив статус завдання {TaskId} з {OldStatus} на {NewStatus}",
                currentUserId,
                taskId,
                oldStatus,
                newStatus);

            return ToModel(entity);
        });
    }

    private static bool CanManage(TaskItem task, Guid userId, ProjectRole? role)
    {
        return task.OwnerId == userId || role == ProjectRole.Owner;
    }

    /// <summary>
    /// Видаляти завдання та змінювати дедлайн може його власник або власник проєкту.
    /// </summary>
    private async Task<bool> CanManageAsync(TaskItem task, Guid userId)
    {
        if (task.OwnerId == userId)
        {
            return true;
        }

        var role = await this.projectRepository.GetMemberRoleAsync(task.ProjectId, userId);
        return CanManage(task, userId, role);
    }

    private static TaskModel ToModel(TaskItem entity)
    {
        return new TaskModel
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            TaskCode = entity.TaskCode,
            Title = entity.Title,
            Description = entity.Description,
            Deadline = entity.Deadline,
            Priority = entity.Priority,
            CategoryId = entity.CategoryId,
            Status = entity.Status,
            OwnerId = entity.OwnerId,
        };
    }

    private void ValidateNewTask(TaskModel model)
    {
        this.ValidateCondition(model != null, "Дані завдання не передано.");
        this.ValidateNotEmpty(model!.Title, "Назва");
        this.ValidateCondition(model.Title.Trim().Length <= MaxTitleLength, "Назва занадто довга.");
        this.ValidateCondition(model.ProjectId != Guid.Empty, "Не вказано проєкт.");
        this.ValidateCondition(model.OwnerId != Guid.Empty, "Не вказано власника завдання.");
        this.ValidateCondition(model.Deadline.HasValue, "Дедлайн не вказано.");
        this.ValidateCondition(model.Deadline!.Value.ToUniversalTime() > DateTime.UtcNow, "Дедлайн не може бути в минулому.");
    }

    private void ValidateUpdateTask(TaskModel model)
    {
        this.ValidateCondition(model != null, "Дані завдання не передано.");
        this.ValidateCondition(model!.Id != Guid.Empty, "Ідентифікатор завдання не вказано.");
        this.ValidateNotEmpty(model.Title, "Назва");
        this.ValidateCondition(model.Title.Trim().Length <= MaxTitleLength, "Назва занадто довга.");

        if (model.Deadline.HasValue)
        {
            this.ValidateCondition(model.Deadline.Value.ToUniversalTime() > DateTime.UtcNow, "Дедлайн не може бути в минулому.");
        }
    }

    private async Task<TaskItem> BuildEntityAsync(TaskModel model)
    {
        var tasksInProject = await this.taskRepository.CountByProjectAsync(model.ProjectId);

        return new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = model.ProjectId,
            OwnerId = model.OwnerId,
            CreatorId = model.OwnerId,
            TaskCode = $"{TaskCodePrefix}{tasksInProject + 1}",
            Title = model.Title.Trim(),
            Description = model.Description,
            Deadline = model.Deadline!.Value.ToUniversalTime(),
            Priority = model.Priority,
            CategoryId = model.CategoryId,
            Status = TaskItemStatus.Todo,
        };
    }
}