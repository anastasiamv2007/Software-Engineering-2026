namespace SoftwareEngineering.BLL.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Duplicate,
    Forbidden,
    Unexpected,
}

public sealed record Error(ErrorType Type, string Message);

public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        this.IsSuccess = isSuccess;
        this.Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !this.IsSuccess;

    public Error? Error { get; }

    public static Result Ok() => new(true, null);

    public static Result Fail(ErrorType type, string message) => new(false, new Error(type, message));

    public static Result<T> Ok<T>(T value) => new(value, true, null);

    public static Result<T> Fail<T>(ErrorType type, string message) => new(default, false, new Error(type, message));
}

public sealed class Result<T> : Result
{
    internal Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        this.Value = value;
    }

    public T? Value { get; }
}