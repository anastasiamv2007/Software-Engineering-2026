using Moq;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class TaskCodeTests
{
    private readonly Mock<ITaskRepository> taskRepositoryMock = new();
    private readonly Mock<IProjectRepository> projectRepositoryMock = new();
    private readonly Mock<ICategoryRepository> categoryRepositoryMock = new();

    public TaskCodeTests()
    {
        this.projectRepositoryMock
            .Setup(repository => repository.GetMemberRoleAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(ProjectRole.Owner);
    }

    [Fact]
    public void NextTaskCode_EmptyProject_ReturnsFirstCode()
    {
        // Act
        var code = TaskService.NextTaskCode(Array.Empty<string>());

        // Assert
        Assert.Equal("PL-1", code);
    }

    // Головний баг: PL-2 видалили, лишились PL-1 і PL-3 — кількість (2) дала б зайнятий PL-3
    [Fact]
    public void NextTaskCode_AfterDeletedTask_ReturnsNumberAfterHighest()
    {
        // Act
        var code = TaskService.NextTaskCode(new[] { "PL-1", "PL-3" });

        // Assert
        Assert.Equal("PL-4", code);
    }

    // Порядок у БД не гарантований, а «PL-10» за рядком менший за «PL-9» — порівнюємо числа
    [Fact]
    public void NextTaskCode_UnorderedAndMultiDigitCodes_ComparesNumbersNotStrings()
    {
        // Act
        var code = TaskService.NextTaskCode(new[] { "PL-9", "PL-10", "PL-2" });

        // Assert
        Assert.Equal("PL-11", code);
    }

    // Старі або вручну внесені коди іншого формату не ламають генерацію
    [Fact]
    public void NextTaskCode_IgnoresCodesInOtherFormat()
    {
        // Act
        var code = TaskService.NextTaskCode(new[] { "TASK-50", "PL-abc", "PL-2" });

        // Assert
        Assert.Equal("PL-3", code);
    }

    // Код рахує репозиторій під блокуванням проєкту, але правило «найбільший номер + 1» дає сервіс
    [Fact]
    public async Task CreateAsync_PassesNextTaskCodeRuleToRepository()
    {
        // Arrange
        Func<IReadOnlyList<string>, string>? generator = null;
        this.taskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>(), It.IsAny<Func<IReadOnlyList<string>, string>>()))
            .Callback<TaskItem, Func<IReadOnlyList<string>, string>>((_, generateCode) => generator = generateCode)
            .Returns(Task.CompletedTask);
        var service = this.CreateService();

        // Act
        await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.NotNull(generator);
        Assert.Equal("PL-8", generator!(new[] { "PL-1", "PL-7" }));
    }

    private static TaskModel CreateValidModel()
    {
        return new TaskModel
        {
            ProjectId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Нове завдання",
            Deadline = DateTime.UtcNow.AddDays(2),
        };
    }

    private TaskService CreateService()
    {
        return new TaskService(this.taskRepositoryMock.Object, this.projectRepositoryMock.Object, this.categoryRepositoryMock.Object);
    }
}
