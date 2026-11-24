namespace SoftwareEngineering.BLL.Models;

/// <summary>
/// Що користувач змінює при копіюванні; решта (опис, пріоритет, категорія) береться з оригіналу.
/// </summary>
public class DuplicateTaskModel
{
    public string Title { get; set; } = string.Empty;

    public DateTime? Deadline { get; set; }

    public bool CopySubtasks { get; set; } = true;
}
