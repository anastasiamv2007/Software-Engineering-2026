namespace SoftwareEngineering.BLL.Models;

public class ProjectModel : BaseModel
{
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TasksCount { get; set; }
}

public class CreateProjectModel
{
    public string Name { get; set; } = string.Empty;
    public Guid? OwnerId { get; set; }
}

public class UpdateProjectModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}