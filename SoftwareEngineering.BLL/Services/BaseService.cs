using System;
using System.Threading.Tasks;
using Serilog;
using SoftwareEngineering.BLL.Exceptions;

namespace SoftwareEngineering.BLL.Services;

/// <summary>
/// Базовий клас для сервісів BLL, що стандартизує логування дій користувача, помилок та валідацію.
/// </summary>
public abstract class BaseService
{
    protected ILogger Logger => Log.ForContext(this.GetType());

    /// <summary>
    /// Перевіряє, що текстове поле не є порожнім.
    /// </summary>
    protected void ValidateNotEmpty(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            var message = $"Поле '{fieldName}' не може бути порожнім.";
            this.Logger.Warning("Помилка валідації: {Message}", message);
            throw new ValidationException(message);
        }
    }

    /// <summary>
    /// Перевіряє довільну бізнес-умову. Якщо умова хибна — логує попередження і викидає ValidationException.
    /// </summary>
    protected void ValidateCondition(bool condition, string errorMessage)
    {
        if (!condition)
        {
            this.Logger.Warning("Помилка валідації бізнес-правила: {ErrorMessage}", errorMessage);
            throw new ValidationException(errorMessage);
        }
    }

    /// <summary>
    /// Виконує асинхронну дію сервісу з автоматичним логуванням початку, успішного завершення та помилок.
    /// </summary>
    protected async Task<T> ExecuteWithLoggingAsync<T>(string actionName, Func<Task<T>> action)
    {
        try
        {
            this.Logger.Information("Початок виконання дії: {ActionName}", actionName);
            var result = await action();
            this.Logger.Information("Успішно виконано дію: {ActionName}", actionName);
            return result;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час виконання дії: {ActionName}", actionName);
            throw;
        }
    }
}