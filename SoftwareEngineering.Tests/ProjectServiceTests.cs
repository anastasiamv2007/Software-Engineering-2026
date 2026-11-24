using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class ProjectServiceTests
{
    private readonly Mock<IProjectRepository> repositoryMock = new();
    private Project? savedProject;

    // ==========================================
    // 1. CREATE TESTS
    // ==========================================

    [Fact]
    public async Task CreateProjectAsync_ValidModel_ReturnsModelWithNewId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var service = this.CreateService();
        var model = CreateValidModel(userId);
        this.CaptureSavedProject();

        // Act
        var result = await service.CreateProjectAsync(model, userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id);
        Assert.Equal(model.Name, result.Value.Name);
        this.repositoryMock.Verify(repo => repo.AddAsync(It.IsAny<Project>()), Times.Once);
    }

    // Автор проєкту одразу стає його учасником з роллю Owner
    [Fact]
    public async Task CreateProjectAsync_ValidModel_AddsOwnerAsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        this.CaptureSavedProject();
        var service = this.CreateService();

        // Act
        await service.CreateProjectAsync(CreateValidModel(userId), userId);

        // Assert
        var member = Assert.Single(this.savedProject!.Members);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(DAL.Enums.ProjectRole.Owner, member.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateProjectAsync_EmptyName_ReturnsValidation(string? invalidName)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var model = CreateValidModel(userId);
        model.Name = invalidName!;
        var service = this.CreateService();

        // Act
        var result = await service.CreateProjectAsync(model, userId);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.AddAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task CreateProjectAsync_EmptyOwnerId_ReturnsValidation()
    {
        // Arrange
        var model = CreateValidModel(Guid.Empty);
        var service = this.CreateService();

        // Act
        var result = await service.CreateProjectAsync(model, Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task CreateProjectAsync_NameAlreadyExists_ReturnsDuplicate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        this.repositoryMock
            .Setup(repo => repo.FindByNameAsync("Новий проєкт"))
            .ReturnsAsync(new Project { Id = Guid.NewGuid(), Name = "Новий проєкт" });
        var service = this.CreateService();

        // Act
        var result = await service.CreateProjectAsync(CreateValidModel(userId), userId);

        // Assert
        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.AddAsync(It.IsAny<Project>()), Times.Never);
    }

    // ==========================================
    // 2. READ TESTS
    // ==========================================

    [Fact]
    public async Task GetUserProjectsAsync_ValidUserId_ReturnsProjectModels()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var projectEntities = new List<Project>
        {
            new Project { Id = Guid.NewGuid(), Name = "Проєкт 1", OwnerId = userId },
            new Project { Id = Guid.NewGuid(), Name = "Проєкт 2", OwnerId = userId }
        };

        this.repositoryMock
            .Setup(repo => repo.GetAllByUserIdAsync(userId))
            .ReturnsAsync(projectEntities);

        var service = this.CreateService();

        // Act
        var result = await service.GetUserProjectsAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        var list = result.Value!.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Проєкт 1", list[0].Name);
    }

    [Fact]
    public async Task GetUserProjectsAsync_EmptyUserId_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.GetUserProjectsAsync(Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.GetAllByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ProjectNotFound_ReturnsNotFound()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    // ==========================================
    // 3. UPDATE TESTS
    // ==========================================

    [Fact]
    public async Task UpdateProjectAsync_ValidModel_CallsRepositoryUpdate()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var validModel = new UpdateProjectModel
        {
            Id = projectId,
            Name = "Оновлена назва"
        };

        var existingEntity = new Project
        {
            Id = projectId,
            Name = "Стара назва",
            OwnerId = ownerId
        };

        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(projectId))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();

        // Act
        var result = await service.UpdateProjectAsync(validModel, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
        this.repositoryMock.Verify(repo => repo.UpdateAsync(It.Is<Project>(p => p.Id == projectId && p.Name == "Оновлена назва")), Times.Once);
    }

    [Fact]
    public async Task UpdateProjectAsync_EmptyName_ReturnsValidation()
    {
        // Arrange
        var invalidModel = new UpdateProjectModel { Id = Guid.NewGuid(), Name = string.Empty };
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProjectAsync(invalidModel, Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.UpdateAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProjectAsync_ProjectNotFound_ReturnsNotFound()
    {
        // Arrange
        var model = new UpdateProjectModel { Id = Guid.NewGuid(), Name = "Valid Name" };
        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(model.Id))
            .ReturnsAsync((Project?)null);
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProjectAsync(model, Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    // Перейменувати на назву іншого проєкту не можна, а залишити свою — можна
    [Fact]
    public async Task UpdateProjectAsync_NameOfAnotherProject_ReturnsDuplicate()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), Name = "Мій", OwnerId = ownerId };
        this.repositoryMock.Setup(repo => repo.GetByIdAsync(project.Id)).ReturnsAsync(project);
        this.repositoryMock
            .Setup(repo => repo.FindByNameAsync("Чужий"))
            .ReturnsAsync(new Project { Id = Guid.NewGuid(), Name = "Чужий" });
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProjectAsync(new UpdateProjectModel { Id = project.Id, Name = "Чужий" }, ownerId);

        // Assert
        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.UpdateAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProjectAsync_SameNameAsItself_Succeeds()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var project = new Project { Id = Guid.NewGuid(), Name = "Мій", OwnerId = ownerId };
        this.repositoryMock.Setup(repo => repo.GetByIdAsync(project.Id)).ReturnsAsync(project);
        this.repositoryMock.Setup(repo => repo.FindByNameAsync("Мій")).ReturnsAsync(project);
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProjectAsync(new UpdateProjectModel { Id = project.Id, Name = "Мій" }, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
    }

    // ==========================================
    // 4. DELETE TESTS
    // ==========================================

    [Fact]
    public async Task DeleteProjectAsync_ValidId_CallsRepositoryDelete()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingEntity = new Project { Id = projectId, Name = "Проєкт для видалення", OwnerId = ownerId };

        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(projectId))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();

        // Act
        var result = await service.DeleteProjectAsync(projectId, ownerId);

        // Assert
        Assert.True(result.IsSuccess);
        this.repositoryMock.Verify(repo => repo.DeleteAsync(projectId), Times.Once);
    }

    [Fact]
    public async Task DeleteProjectAsync_ProjectNotFound_ReturnsNotFound()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(projectId))
            .ReturnsAsync((Project?)null);
        var service = this.CreateService();

        // Act
        var result = await service.DeleteProjectAsync(projectId, Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ==========================================
    // 5. ПРАВА: ЛИШЕ ВЛАСНИК
    // ==========================================

    [Fact]
    public async Task UpdateProjectAsync_UserIsNotOwner_ReturnsForbidden()
    {
        // Arrange
        var project = new Project { Id = Guid.NewGuid(), Name = "Чужий проєкт", OwnerId = Guid.NewGuid() };
        this.repositoryMock.Setup(repo => repo.GetByIdAsync(project.Id)).ReturnsAsync(project);
        var service = this.CreateService();

        // Act
        var result = await service.UpdateProjectAsync(new UpdateProjectModel { Id = project.Id, Name = "Нова назва" }, Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.UpdateAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProjectAsync_UserIsNotOwner_ReturnsForbidden()
    {
        // Arrange
        var project = new Project { Id = Guid.NewGuid(), Name = "Чужий проєкт", OwnerId = Guid.NewGuid() };
        this.repositoryMock.Setup(repo => repo.GetByIdAsync(project.Id)).ReturnsAsync(project);
        var service = this.CreateService();

        // Act
        var result = await service.DeleteProjectAsync(project.Id, Guid.NewGuid());

        // Assert
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ==========================================
    // 6. ПРОЄКТИ ДЛЯ ФОРМИ СТВОРЕННЯ ЗАВДАННЯ
    // ==========================================

    // У списку лише проєкти, де користувач може створювати завдання: читач свій проєкт там не побачить
    [Fact]
    public async Task GetProjectsForNewTaskAsync_ReturnsOnlyOwnerAndCoExecutorProjects()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var owned = new Project { Id = Guid.NewGuid(), Name = "Мій", OwnerId = userId };
        var shared = new Project { Id = Guid.NewGuid(), Name = "Командний", OwnerId = Guid.NewGuid() };
        var readOnly = new Project { Id = Guid.NewGuid(), Name = "Лише читаю", OwnerId = Guid.NewGuid() };
        this.repositoryMock.Setup(repo => repo.GetAllByUserIdAsync(userId)).ReturnsAsync(new[] { owned, shared, readOnly });
        this.repositoryMock.Setup(repo => repo.GetMemberRoleAsync(owned.Id, userId)).ReturnsAsync(ProjectRole.Owner);
        this.repositoryMock.Setup(repo => repo.GetMemberRoleAsync(shared.Id, userId)).ReturnsAsync(ProjectRole.CoExecutor);
        this.repositoryMock.Setup(repo => repo.GetMemberRoleAsync(readOnly.Id, userId)).ReturnsAsync(ProjectRole.Reviewer);
        var service = this.CreateService();

        // Act
        var result = await service.GetProjectsForNewTaskAsync(userId);

        // Assert
        Assert.Equal(new[] { "Командний", "Мій" }, result.Value!.Select(project => project.Name));
    }

    [Fact]
    public async Task GetProjectsForNewTaskAsync_NoProjects_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        this.repositoryMock.Setup(repo => repo.GetAllByUserIdAsync(userId)).ReturnsAsync(Array.Empty<Project>());
        var service = this.CreateService();

        // Act
        var result = await service.GetProjectsForNewTaskAsync(userId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task GetProjectsForNewTaskAsync_EmptyUserId_ReturnsValidation()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.GetProjectsForNewTaskAsync(Guid.Empty);

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.repositoryMock.Verify(repo => repo.GetAllByUserIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ==========================================
    // ХЕЛПЕРИ
    // ==========================================

    private static CreateProjectModel CreateValidModel(Guid ownerId)
    {
        return new CreateProjectModel
        {
            Name = "Новий проєкт",
            OwnerId = ownerId
        };
    }

    private ProjectService CreateService()
    {
        return new ProjectService(this.repositoryMock.Object);
    }

    private void CaptureSavedProject()
    {
        this.repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Project>()))
            .Callback<Project>(project => this.savedProject = project)
            .Returns(Task.CompletedTask);
    }
}
