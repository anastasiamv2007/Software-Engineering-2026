using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ITaskRepository
{
    Task<int> CountByProjectAsync(Guid projectId);

    Task AddAsync(TaskItem task);

    Task<IEnumerable<TaskItem>> GetAsync();

    // 2
    Task<TaskItem?> GetByIdAsync(Guid id);

    // 3
    Task UpdateAsync(TaskItem task);

    Task DeleteAsync(TaskItem entity);

    // 5
    Task DeleteAsync(Guid id);

}