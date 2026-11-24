using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Tests;

/// <summary>
/// Контракт BaseService: очікувані відмови повертаються через Fail, а винятки сервіс не ховає —
/// їх обробляє глобальний обробник у UI.
/// </summary>
public class BaseServiceTests
{
    private readonly TestService service = new();

    [Fact]
    public void Fail_ReturnsFailureWithSameTypeAndMessage()
    {
        // Act
        var result = this.service.Reject(ErrorType.Forbidden, "Немає прав.");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal("Немає прав.", result.Error.Message);
    }

    [Fact]
    public void FailGeneric_ReturnsFailureWithoutValue()
    {
        // Act
        var result = this.service.Reject<int>(ErrorType.NotFound, "Не знайдено.");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(default, result.Value);
    }

    // Загального try/catch у сервісах немає: збій доходить до глобального обробника в App, а не стає Result
    [Fact]
    public async Task ServiceMethod_DoesNotSwallowExceptions()
    {
        // Act + Assert
        await Assert.ThrowsAsync<TimeoutException>(() => this.service.RunAsync(() => throw new TimeoutException()));
    }

    private sealed class TestService : BaseService
    {
        public Result Reject(ErrorType type, string message) => this.Fail(type, message);

        public Result<T> Reject<T>(ErrorType type, string message) => this.Fail<T>(type, message);

        public async Task<Result> RunAsync(Func<Task> action)
        {
            await action();
            return Result.Ok();
        }
    }
}
