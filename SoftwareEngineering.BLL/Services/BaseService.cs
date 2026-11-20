using Serilog;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Базовий клас сервісів BLL. Єдине місце, де сервіси перехоплюють винятки:
/// очікувані помилки (валідація, права, «не знайдено») методи повертають як Result.Fail,
/// а непередбачені (наприклад, недоступна БД) ExecuteAsync перетворює на ErrorType.Unexpected.
/// </summary>
public abstract class BaseService
{
    protected const string UnexpectedErrorMessage = "Сталася внутрішня помилка. Спробуйте ще раз.";

    protected ILogger Logger => Log.ForContext(this.GetType());

    /// <summary>
    /// Виконує дію сервісу: логує результат у Seq і не випускає винятки назовні.
    /// </summary>
    protected async Task<Result<T>> ExecuteAsync<T>(string actionName, Func<Task<Result<T>>> action)
    {
        try
        {
            this.Logger.Debug("Початок виконання дії: {ActionName}", actionName);
            var result = await action();
            this.LogOutcome(actionName, result);
            return result;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час виконання дії: {ActionName}", actionName);
            return Result.Fail<T>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    /// <inheritdoc cref="ExecuteAsync{T}(string, Func{Task{Result{T}}})"/>
    protected async Task<Result> ExecuteAsync(string actionName, Func<Task<Result>> action)
    {
        try
        {
            this.Logger.Debug("Початок виконання дії: {ActionName}", actionName);
            var result = await action();
            this.LogOutcome(actionName, result);
            return result;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час виконання дії: {ActionName}", actionName);
            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    // Відмова (валідація, права, «не знайдено») — Warning з типом і текстом, щоб у Seq було видно причину
    private void LogOutcome(string actionName, Result result)
    {
        if (result.IsFailure)
        {
            this.Logger.Warning(
                "Дію {ActionName} відхилено ({ErrorType}): {ErrorMessage}",
                actionName,
                result.Error!.Type,
                result.Error.Message);
        }
        else
        {
            this.Logger.Information("Успішно виконано дію: {ActionName}", actionName);
        }
    }
}
