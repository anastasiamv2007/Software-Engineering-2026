namespace SoftwareEngineering.DAL.Entities;

public class Comment
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public Guid AuthorId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;

    public User Author { get; set; } = null!;
}