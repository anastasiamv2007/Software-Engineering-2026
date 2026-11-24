using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models;

public class ReviewTaskItemModel
{
    public Guid TaskId { get; set; }

    public Guid ProjectId { get; set; }

    public string TaskCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? CategoryName { get; set; }

    public string? CategoryColorHex { get; set; }

    public TaskPriority Priority { get; set; }

    public DateTime? Deadline { get; set; }

    public ProjectRole ReviewerRole { get; set; }
}