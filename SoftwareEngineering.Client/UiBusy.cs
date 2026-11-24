using System.Windows;

namespace SoftwareEngineering.Client;

/// <summary>
/// Блокує елементи вікна на час операції: using (UiBusy.Disable(button)) { await ... }.
/// Коли блок using закінчується, елементи вмикаються назад — навіть якщо всередині вилетів виняток
/// (його далі обробить глобальний обробник в App). Так кнопка не лишиться вимкненою назавжди, і try/finally не потрібен.
/// </summary>
internal sealed class UiBusy : IDisposable
{
    private readonly Action release;

    private UiBusy(Action release)
    {
        this.release = release;
    }

    public static UiBusy Disable(params UIElement[] elements)
    {
        foreach (var element in elements)
        {
            element.IsEnabled = false;
        }

        return new UiBusy(() =>
        {
            foreach (var element in elements)
            {
                element.IsEnabled = true;
            }
        });
    }

    /// <summary>
    /// Загальний варіант: enter виконується одразу, exit — коли закінчиться блок using.
    /// </summary>
    public static UiBusy Scope(Action enter, Action exit)
    {
        enter();
        return new UiBusy(exit);
    }

    public void Dispose()
    {
        this.release();
    }
}
