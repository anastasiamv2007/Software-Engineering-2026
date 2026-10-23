using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class TaskService : BaseService, ITaskService
{
    private const int MaxTitleLength = 200;
    private const string TaskCodePrefix = "PL-";

    private readonly ITaskRepository taskRepository;

    public TaskService(ITaskRepository taskRepository)
    {
        this.taskRepository = taskRepository;
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
    public async Task<TaskModel> UpdateAsync(TaskModel model)
    {
        return await this.ExecuteWithLoggingAsync("Редагування завдання", async () =>
        {
            this.ValidateUpdateTask(model);

            var entity = await this.taskRepository.GetByIdAsync(model.Id);
            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");

            entity!.Title = model.Title.Trim();
            entity.Description = model.Description;
            entity.Deadline = model.Deadline?.ToUniversalTime();
            entity.Priority = model.Priority;
            entity.CategoryId = model.CategoryId;

            await this.taskRepository.UpdateAsync(entity);

            return ToModel(entity);
        });
    }

    public async Task<TaskModel> DeleteTaskByOwnerAsync(TaskModel model, Guid currentUserId)
    {
        await this.ExecuteWithLoggingAsync("Видалення завдання", async () =>
        {
            this.ValidateCondition(currentUserId != Guid.Empty, "Ідентифікатор завдання не вказано.");

            var entity = await this.taskRepository.GetByIdAsync(currentUserId);
            this.ValidateCondition(entity != null, "Завдання з таким ідентифікатором не знайдено.");

            // Місце під Activity Log / перевірку прав
            // ...

            await this.taskRepository.DeleteAsync(entity!);
            return true;
        });
        return model;
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
        };
    }

    private void ValidateNewTask(TaskModel model)
    {
        this.ValidateCondition(model != null, "Дані завдання не передано.");
        this.ValidateNotEmpty(model!.Title, "Назва");
        this.ValidateCondition(model.Title.Trim().Length <= MaxTitleLength, "Назва занадто довга.");
        this.ValidateCondition(model.ProjectId != Guid.Empty, "Не вказано проєкт.");
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