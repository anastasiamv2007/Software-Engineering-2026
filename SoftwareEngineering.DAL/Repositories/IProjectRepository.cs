using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface IProjectRepository
{
    Task<Project?> FindByNameAsync(string name);

    Task<Guid?> GetDefaultOwnerIdAsync();

    Task AddAsync(Project project);
}