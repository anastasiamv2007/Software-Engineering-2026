using System.Runtime.CompilerServices;
using Serilog;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Базовий клас сервісів BLL.
/// Очікувані відмови (валідація, права, «не знайдено») сервіс повертає через Fail — вони одразу логуються як Warning
/// там, де виникли. Винятки сервіси не перехоплюють: непередбачена помилка (наприклад, недоступна БД)
/// доходить до глобального обробника в App, який логує її як Error і показує повідомлення.
/// </summary>
public abstract class BaseService
{
    protected ILogger Logger => Log.ForContext(this.GetType());

    /// <summary>
    /// Повертає відмову і логує її як Warning з назвою методу, у якому вона виникла.
    /// </summary>
    protected Result Fail(ErrorType type, string message, [CallerMemberName] string action = "")
    {
        this.LogFailure(action, type, message);
        return Result.Fail(type, message);
    }

    /// <inheritdoc cref="Fail(ErrorType, string, string)"/>
    protected Result<T> Fail<T>(ErrorType type, string message, [CallerMemberName] string action = "")
    {
        this.LogFailure(action, type, message);
        return Result.Fail<T>(type, message);
    }

    private void LogFailure(string action, ErrorType type, string message)
    {
        this.Logger.Warning("Дію {Action} відхилено ({ErrorType}): {ErrorMessage}", action, type, message);
    }
}
