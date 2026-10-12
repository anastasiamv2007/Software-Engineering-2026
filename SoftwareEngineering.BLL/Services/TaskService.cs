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