using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Models;

public class CommentModel
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public Guid AuthorId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    // null — автор уже не учасник проєкту (наприклад, його видалили з команди)
    public ProjectRole? AuthorRole { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    // Чи може поточний користувач видалити коментар: автор або власник проєкту
    public bool CanDelete { get; set; }
}
