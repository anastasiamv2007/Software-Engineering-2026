using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models;

public class TaskFilterCriteria
{
    public TaskItemStatus? Status { get; set; }

    public TaskPriority? Priority { get; set; }

    // Тип збігається з TaskModel.CategoryId (categories.id — int)
    public int? CategoryId { get; set; }

    public bool OnlyOverdue { get; set; }

    // За замовчуванням сортуємо за дедлайном
    public TaskSortOption SortBy { get; set; } = TaskSortOption.ByDeadlineAsc;
}
