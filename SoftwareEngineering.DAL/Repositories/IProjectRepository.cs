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

    Task UpdateMemberRoleAsync(Guid projectId, Guid userId, ProjectRole role);
    Task UpdateAsync(Project project);
    Task DeleteAsync(Guid id);

    Task AddMemberAsync(ProjectMember member);
    Task<IReadOnlyList<User>> GetProjectUsersAsync(Guid projectId);
    Task<IReadOnlyList<ProjectMember>> GetMembersAsync(Guid projectId);
    Task<int> RemoveMemberAsync(Guid projectId, Guid userId);

    /// <summary>
    /// Атомарно передає власність. Повертає false, якщо проєкт не знайдено або новий власник не є його учасником.
    /// </summary>
    Task<bool> TransferOwnershipAsync(Guid projectId, Guid newOwnerId);
}