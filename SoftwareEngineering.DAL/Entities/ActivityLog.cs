namespace SoftwareEngineering.DAL.Entities;

public class ActivityLog
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;
}