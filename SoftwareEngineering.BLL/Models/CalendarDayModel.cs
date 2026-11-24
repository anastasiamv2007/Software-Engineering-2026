namespace SoftwareEngineering.BLL.Models;

/// <summary>
/// Одна клітинка місячного календаря.
/// </summary>
public class CalendarDayModel
{
    public DateTime Date { get; init; }

    public int Day => this.Date.Day;

    /// <summary>
    /// false — день сусіднього місяця, яким доповнено перший і останній тиждень.
    /// </summary>
    public bool IsCurrentMonth { get; init; }

    public bool IsToday { get; init; }

    /// <summary>
    /// День, на який користувач клацнув (його завдання показано праворуч).
    /// </summary>
    public bool IsSelected { get; init; }

    /// <summary>
    /// Усі завдання з дедлайном у цей день, за часом дедлайну.
    /// </summary>
    public IReadOnlyList<TaskModel> Tasks { get; init; } = Array.Empty<TaskModel>();

    /// <summary>
    /// Перші кілька завдань, які вміщаються в клітинку.
    /// </summary>
    public IReadOnlyList<TaskModel> PreviewTasks { get; init; } = Array.Empty<TaskModel>();

    /// <summary>
    /// Скільки завдань не вмістилось у клітинку («+2 ще»).
    /// </summary>
    public int HiddenTasksCount => this.Tasks.Count - this.PreviewTasks.Count;

    public bool HasOverdue { get; init; }
}
