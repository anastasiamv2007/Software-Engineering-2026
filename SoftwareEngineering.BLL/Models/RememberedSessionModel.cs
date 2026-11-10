namespace SoftwareEngineering.BLL.Models;

/// <summary>
/// Збережений вхід для конкретної бази на цьому ПК. Пароль не зберігається:
/// лише користувач і відбиток його хешу пароля (зміна пароля скасовує збережений вхід).
/// </summary>
public class RememberedSessionModel
{
    public Guid UserId { get; set; }

    public string PasswordStamp { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
