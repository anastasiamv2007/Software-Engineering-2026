using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IProjectMemberService : IService
{
    /// <summary>
    /// Додає користувача (за email) до проєкту з роллю CoExecutor. Додавати може лише Owner проєкту.
    /// </summary>
    Task<Result> AddCoExecutorAsync(Guid projectId, string email, Guid currentUserId);

    /// <summary>
    /// Повертає список усіх учасників проєкту разом із їхніми ролями.
    /// </summary>
    Task<IReadOnlyList<ProjectMemberModel>> GetProjectMembersAsync(Guid projectId);

    /// <summary>
    /// Змінює роль учасника проєкту між CoExecutor та Reviewer. Змінювати роль може лише Owner проєкту.
    /// </summary>
    Task<Result> ChangeRoleAsync(Guid projectId, Guid memberId, ProjectRole newRole, Guid currentUserId);
}
