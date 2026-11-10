namespace SoftwareEngineering.DAL.Entities;

public class Subtask
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string Title { get; set; } = null!;

    public bool IsCompleted { get; set; }

    public TaskItem Task { get; set; } = null!;
}