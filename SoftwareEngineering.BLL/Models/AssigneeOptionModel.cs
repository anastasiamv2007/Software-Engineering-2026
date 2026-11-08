namespace SoftwareEngineering.BLL.Models;

public class AssigneeOptionModel
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}