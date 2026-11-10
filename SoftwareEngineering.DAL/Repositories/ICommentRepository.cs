using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ICommentRepository
{
    /// <summary>
    /// Коментарі завдання від старіших до новіших, разом з автором.
    /// </summary>
    Task<IReadOnlyList<Comment>> GetByTaskIdAsync(Guid taskId);

    Task<Comment?> GetByIdAsync(Guid id);

    Task AddAsync(Comment comment);

    Task DeleteAsync(Guid id);
}
