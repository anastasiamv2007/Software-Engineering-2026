using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ITaskRepository
{
    /// <summary>
    /// Зберігає нове завдання разом з його підзадачами і присвоює йому код.
    /// generateCode отримує коди всіх наявних завдань проєкту і повертає код для нового.
    /// Проєкт блокується до кінця збереження, тому два одночасні створення в одному проєкті
    /// виконуються по черзі й не можуть отримати однаковий код.
    /// </summary>
    Task AddAsync(TaskItem task, Func<IReadOnlyList<string>, string> generateCode);

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