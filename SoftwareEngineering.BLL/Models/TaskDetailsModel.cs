using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models;

/// <summary>
/// Повна інформація про завдання для сторінки деталей.
/// </summary>
public class TaskDetailsModel
{
    public TaskModel Task { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public bool IsOverdue { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectOwnerName { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string OwnerEmail { get; set; } = string.Empty;

    public string? CreatorName { get; set; }

    public string? AssigneeName { get; set; }

    public string? CategoryName { get; set; }

    public string? CategoryColorHex { get; set; }

    public int SubtasksTotal { get; set; }

    public int SubtasksCompleted { get; set; }

    public int CommentsCount { get; set; }

    public IReadOnlyList<TaskMemberModel> Members { get; set; } = new List<TaskMemberModel>();

    // Права поточного користувача — щоб UI одразу показав, що він може робити із завданням
    public ProjectRole? CurrentUserRole { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDeleteOrChangeDeadline { get; set; }
}

public class TaskMemberModel
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public ProjectRole Role { get; set; }
}
