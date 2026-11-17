using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;

namespace SoftwareEngineering.Tests;

/// <summary>
/// ExecuteAsync — єдине місце, де сервіси перехоплюють винятки. Перевіряємо його контракт напряму.
/// </summary>
public class BaseServiceTests
{
    private readonly TestService service = new();

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsValueUnchanged()
    {
        // Act
        var result = await this.service.Run(() => Task.FromResult(Result.Ok(42)));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    // Очікувана відмова (валідація, права) проходить як є, з тим самим типом і текстом
    [Fact]
    public async Task ExecuteAsync_ExpectedFailure_IsReturnedAsIs()
    {
        // Act
        var result = await this.service.Run(() => Task.FromResult(Result.Fail<int>(ErrorType.Forbidden, "Немає прав.")));

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal("Немає прав.", result.Error.Message);
    }

    // Будь-який виняток усередині дії не вилітає назовні, а стає Unexpected без технічних деталей
    [Fact]
    public async Task ExecuteAsync_ActionThrows_ReturnsUnexpectedWithoutDetails()
    {
        // Act
        var result = await this.service.Run<int>(() => throw new InvalidOperationException("Npgsql: connection refused"));

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
        Assert.DoesNotContain("Npgsql", result.Error.Message);
    }

    // Те саме для дій без значення (Result без T)
    [Fact]
    public async Task ExecuteAsync_NonGenericActionThrows_ReturnsUnexpected()
    {
        // Act
        var result = await this.service.Run(() => Task.FromException<Result>(new TimeoutException()));

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
    }

    private sealed class TestService : BaseService
    {
        public Task<Result<T>> Run<T>(Func<Task<Result<T>>> action) => this.ExecuteAsync("Тестова дія", action);

        public Task<Result> Run(Func<Task<Result>> action) => this.ExecuteAsync("Тестова дія", action);
    }
}
