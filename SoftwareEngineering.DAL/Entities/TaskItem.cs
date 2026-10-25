using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.DAL.Entities;

public class TaskItem
{
    public Guid Id { get; set; }

    public Guid CreatorId { get; set; }

    public Guid ProjectId { get; set; }

    public string TaskCode { get; set; } = null!;

    public int? CategoryId { get; set; }

    public Guid? AssigneeId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? Deadline { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public bool AllowPublicComments { get; set; }

    public DateTime CreatedAt { get; set; }

    public Project Project { get; set; } = null!;

    public Category? Category { get; set; }

    public User? Assignee { get; set; }

    public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();

    public Guid OwnerId { get; set; }
}