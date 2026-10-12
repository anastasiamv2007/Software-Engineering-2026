using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ITaskRepository
{
    Task<int> CountByProjectAsync(Guid projectId);

    Task AddAsync(TaskItem task);
}