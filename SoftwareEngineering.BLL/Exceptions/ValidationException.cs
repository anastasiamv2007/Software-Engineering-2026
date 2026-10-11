using System;

namespace SoftwareEngineering.BLL.Exceptions;

/// <summary>
/// Виняток, що виникає у разі порушення бізнес-правил або помилки валідації вхідних даних.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
    }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
