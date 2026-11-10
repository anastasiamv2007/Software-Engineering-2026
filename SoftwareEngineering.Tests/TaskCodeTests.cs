using Moq;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Exceptions;
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

    // Код щойно зайняв інший користувач — береться наступний, і завдання все одно створюється
    [Fact]
    public async Task CreateAsync_CodeTakenConcurrently_RetriesWithNextCode()
    {
        // Arrange
        this.taskRepositoryMock
            .SetupSequence(repository => repository.GetTaskCodesAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new[] { "PL-1" })
            .ReturnsAsync(new[] { "PL-1", "PL-2" });
        this.taskRepositoryMock
            .SetupSequence(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new DuplicateTaskCodeException("PL-2", new InvalidOperationException()))
            .Returns(Task.CompletedTask);
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(CreateValidModel());

        // Assert
        Assert.Equal("PL-3", result.TaskCode);
        this.taskRepositoryMock.Verify(repository => repository.GetTaskCodesAsync(It.IsAny<Guid>()), Times.Exactly(2));
    }

    // Якщо конфлікт повторюється, копіювання не падає винятком, а повертає Result з помилкою
    [Fact]
    public async Task DuplicateAsync_CodeAlwaysTaken_ReturnsUnexpectedAfterLimitedAttempts()
    {
        // Arrange
        var source = new TaskItem { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Title = "Оригінал", TaskCode = "PL-1" };
        this.taskRepositoryMock.Setup(repository => repository.GetDetailsAsync(source.Id)).ReturnsAsync(source);
        this.taskRepositoryMock
            .Setup(repository => repository.GetTaskCodesAsync(source.ProjectId))
            .ReturnsAsync(new[] { "PL-1" });
        this.taskRepositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<TaskItem>()))
            .ThrowsAsync(new DuplicateTaskCodeException("PL-2", new InvalidOperationException()));
        var service = this.CreateService();

        // Act
        var result = await service.DuplicateAsync(
            source.Id,
            new DuplicateTaskModel { Title = "Копія", Deadline = DateTime.Now.AddDays(1) },
            Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.Unexpected, result.Error!.Type);
        this.taskRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<TaskItem>()), Times.Exactly(3));
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
