using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);
    Task<IEnumerable<Project>> GetAllByUserIdAsync(Guid userId);
    Task<Project?> FindByNameAsync(string name);

    Task<Guid?> GetDefaultOwnerIdAsync();

    Task AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid id);
}