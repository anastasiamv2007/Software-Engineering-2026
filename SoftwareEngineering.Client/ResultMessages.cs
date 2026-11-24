using System.Windows;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.Client;

/// <summary>
/// Показ помилок з Result у вікнах: замість try/catch у кожному обробнику — одна перевірка.
/// Логувати тут не треба: відмову вже записав у Seq сервіс у момент, коли її повернув (BaseService.Fail).
/// </summary>
internal static class ResultMessages
{
    /// <summary>
    /// Якщо дія не вдалась — показує повідомлення і повертає true.
    /// Використання: <c>if (result.ShowIfFailed("Завдання не створено")) return;</c>
    /// </summary>
    public static bool ShowIfFailed(this Result result, string title)
    {
        if (result.IsSuccess)
        {
            return false;
        }

        // Непередбачена помилка (БД недоступна тощо) — червона іконка; валідація, права, «не знайдено» — попередження
        var icon = result.Error!.Type == ErrorType.Unexpected ? MessageBoxImage.Error : MessageBoxImage.Warning;
        MessageBox.Show(result.Error.Message, title, MessageBoxButton.OK, icon);
        return true;
    }
}
