using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ITaskRepository
{
    Task<int> CountByProjectAsync(Guid projectId);

    Task AddAsync(TaskItem task);

    // 2
    Task<TaskItem?> GetByIdAsync(Guid id);

    // 3
    Task UpdateAsync(TaskItem task);
}