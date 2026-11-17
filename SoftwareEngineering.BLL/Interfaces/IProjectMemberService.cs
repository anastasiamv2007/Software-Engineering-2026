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
    Task<Result<IReadOnlyList<ProjectMemberModel>>> GetMembersAsync(Guid projectId, Guid currentUserId);
    Task<Result> RemoveMemberAsync(Guid projectId, Guid memberId, Guid currentUserId);

    /// <summary>
    /// Змінює роль учасника між CoExecutor та Reviewer (Читач). Змінювати роль може лише Owner проєкту.
    /// </summary>
    Task<Result> ChangeRoleAsync(Guid projectId, Guid memberId, ProjectRole newRole, Guid currentUserId);

    /// <summary>
    /// Передає власність іншому учаснику; колишній власник стає співвиконавцем. Лише для Owner.
    /// </summary>
    Task<Result> TransferOwnershipAsync(Guid projectId, Guid newOwnerId, Guid currentUserId);

    /// <summary>
    /// Учасники, яким Owner може передати власність (усі, крім нього самого).
    /// </summary>
    Task<Result<IReadOnlyList<ProjectMemberModel>>> GetTransferCandidatesAsync(Guid projectId, Guid currentUserId);
}
