using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

/// <summary>
/// Обговорення завдання. Читати й писати можуть учасники проєкту, автор і виконавець завдання;
/// видаляти коментар — його автор або власник проєкту.
/// </summary>
public interface ICommentService : IService
{
    Task<Result<IReadOnlyList<CommentModel>>> GetByTaskAsync(Guid taskId, Guid currentUserId);

    Task<Result<CommentModel>> AddAsync(Guid taskId, string content, Guid currentUserId);

    Task<Result> DeleteAsync(Guid commentId, Guid currentUserId);
}
