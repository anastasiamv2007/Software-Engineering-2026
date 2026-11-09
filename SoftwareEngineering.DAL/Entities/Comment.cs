namespace SoftwareEngineering.DAL.Entities;

using System.ComponentModel.DataAnnotations.Schema;

public class Comment
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public Guid AuthorId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public TaskItem Task { get; set; } = null!;

    public User Author { get; set; } = null!;
    [NotMapped]
    public string AuthorName { get; set; } = "Користувач";

    [NotMapped]
    public string RoleText { get; set; } = "Учасник";
}