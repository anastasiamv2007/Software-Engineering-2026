using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.DAL.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id);
    Task<IEnumerable<Project>> GetAllByUserIdAsync(Guid userId);
    Task<Project?> FindByNameAsync(string name);

    Task AddAsync(Project project);

    /// <summary>
    /// Роль користувача в проєкті: Owner для власника проєкту, інакше роль із project_members; null — не учасник.
    /// </summary>
    Task<ProjectRole?> GetMemberRoleAsync(Guid projectId, Guid userId);

    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid id);
}