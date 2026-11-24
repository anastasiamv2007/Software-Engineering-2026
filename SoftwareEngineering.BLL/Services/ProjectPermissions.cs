using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Правила прав у проєкті, які потрібні кільком сервісам одночасно.
/// </summary>
public static class ProjectPermissions
{
    /// <summary>
    /// Створювати завдання можуть власник і співвиконавці; читач (Reviewer) і сторонні — ні.
    /// </summary>
    public static bool CanCreateTasks(ProjectRole? role)
    {
        return role is ProjectRole.Owner or ProjectRole.CoExecutor;
    }
}
