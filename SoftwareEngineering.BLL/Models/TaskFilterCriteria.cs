using System;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models
{
    public class TaskFilterCriteria
    {
        public TaskItemStatus? Status { get; set; }
        public TaskPriority? Priority { get; set; }
        public Guid? CategoryId { get; set; }
        public bool OnlyOverdue { get; set; }

        // За замовчуванням сортуємо за дедлайном
        public TaskSortOption SortBy { get; set; } = TaskSortOption.ByDeadlineAsc;
    }
}