namespace SoftwareEngineering.DAL.Entities;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string ColorHex { get; set; } = null!;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}