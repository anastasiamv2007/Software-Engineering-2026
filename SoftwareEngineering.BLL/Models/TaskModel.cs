using SoftwareEngineering.DAL.Enums;
using System.Collections.ObjectModel;

namespace SoftwareEngineering.BLL.Models;

public class TaskModel : BaseModel
{
    public Guid ProjectId { get; set; }

    public string TaskCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? Deadline { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public int? CategoryId { get; set; }

    // Лише для відображення: заповнюється, якщо репозиторій підвантажив категорію
    public string? CategoryName { get; set; }

    public string? CategoryColorHex { get; set; }

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    public ObservableCollection<SubtaskModel> Subtasks { get; set; } = new ObservableCollection<SubtaskModel>();

    public Guid OwnerId { get; set; }
}