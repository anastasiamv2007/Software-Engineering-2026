using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models;

public class ProjectMemberModel
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public ProjectRole Role { get; set; }
}