namespace SoftwareEngineering.BLL.Models;

public class SubtaskModel
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}
