using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models;

public class TaskModel : BaseModel
{
    public Guid ProjectId { get; set; }

    public string TaskCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? Deadline { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public int? CategoryId { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
}