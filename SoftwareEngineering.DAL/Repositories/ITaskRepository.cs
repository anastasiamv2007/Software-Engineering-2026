using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ITaskRepository
{
    /// <summary>
    /// Коди всіх наявних завдань проєкту (номери видалених завдань сюди вже не потрапляють).
    /// </summary>
    Task<IReadOnlyList<string>> GetTaskCodesAsync(Guid projectId);

    /// <summary>
    /// Зберігає нове завдання разом з його підзадачами.
    /// Кидає DuplicateTaskCodeException, якщо код уже зайнятий у проєкті.
    /// </summary>
    Task AddAsync(TaskItem task);

    Task<IEnumerable<TaskItem>> GetAsync();

    /// <summary>
    /// Завдання проєктів, де користувач є власником або учасником.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetByUserAsync(Guid userId);

    /// <summary>
    /// Завдання разом із проєктом, учасниками, власником, виконавцем, категорією, підзадачами та коментарями.
    /// </summary>
    Task<TaskItem?> GetDetailsAsync(Guid id);

    // 2
    Task<TaskItem?> GetByIdAsync(Guid id);

    // 3
    Task UpdateAsync(TaskItem task);

    Task DeleteAsync(TaskItem entity);

    // 5
    Task DeleteAsync(Guid id);

}