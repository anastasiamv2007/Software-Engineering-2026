using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.DAL.Entities;

public class ProjectMember
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public ProjectRole Role { get; set; }

    public string? DisplayTitle { get; set; }

    public Project Project { get; set; } = null!;

    public User User { get; set; } = null!;
}