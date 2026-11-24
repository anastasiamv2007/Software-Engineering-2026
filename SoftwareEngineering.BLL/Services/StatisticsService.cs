using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.BLL.Services;

public class StatisticsService : BaseService, IStatisticsService
{
    private readonly ITaskRepository taskRepository;
    private readonly TimeProvider timeProvider;

    public StatisticsService(ITaskRepository taskRepository, TimeProvider timeProvider)
    {
        this.taskRepository = taskRepository;
        this.timeProvider = timeProvider;
    }

    public async Task<Result<TaskStatisticsModel>> GetStatisticsAsync(Guid userId, Guid? projectId = null)
    {
        if (userId == Guid.Empty)
        {
            return this.Fail<TaskStatisticsModel>(ErrorType.Validation, "Ідентифікатор користувача не вказано.");
        }

        if (projectId == Guid.Empty)
        {
            return this.Fail<TaskStatisticsModel>(ErrorType.Validation, "Некоректний ідентифікатор проєкту.");
        }

        var tasks = await this.taskRepository.GetByUserAsync(userId);
        var scoped = projectId.HasValue
            ? tasks.Where(task => task.ProjectId == projectId.Value).ToList()
            : tasks.ToList();

        var now = this.timeProvider.GetUtcNow().UtcDateTime;
        var completed = scoped.Count(task => task.Status == TaskItemStatus.Done);

        return Result.Ok(new TaskStatisticsModel
        {
            Total = scoped.Count,
            Completed = completed,
            InProgress = scoped.Count(task => task.Status == TaskItemStatus.InProgress),
            Active = scoped.Count - completed,
            Overdue = scoped.Count(task =>
                task.Status != TaskItemStatus.Done
                && task.Deadline.HasValue
                && task.Deadline.Value.ToUniversalTime() < now),
        });
    }
}