using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ISubtaskRepository
{
    Task<IReadOnlyList<Subtask>> GetByTaskIdAsync(Guid taskId);

    Task<Subtask?> GetByIdAsync(Guid id);

    Task AddAsync(Subtask subtask);

    Task UpdateAsync(Subtask subtask);

    Task DeleteAsync(Guid id);
}
