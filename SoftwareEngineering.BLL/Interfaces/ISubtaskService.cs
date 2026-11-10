using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

/// <summary>
/// Чек-лист завдання. Переглядати можуть учасники проєкту; додавати й відмічати — власник проєкту,
/// співвиконавці, автор і виконавець завдання; видаляти — автор завдання або власник проєкту.
/// </summary>
public interface ISubtaskService : IService
{
    Task<Result<IReadOnlyList<SubtaskModel>>> GetByTaskAsync(Guid taskId, Guid currentUserId);

    Task<Result<SubtaskModel>> AddAsync(Guid taskId, string title, Guid currentUserId);

    Task<Result<SubtaskModel>> SetCompletedAsync(Guid subtaskId, bool isCompleted, Guid currentUserId);

    Task<Result> DeleteAsync(Guid subtaskId, Guid currentUserId);
}
