using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using SoftwareEngineering.BLL.Exceptions;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
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
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(model.Name, result.Name);
        this.repositoryMock.Verify(repo => repo.AddAsync(It.IsAny<Project>()), Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateProjectAsync_EmptyName_ThrowsValidationException(string? invalidName)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var model = CreateValidModel(userId);
        model.Name = invalidName!;
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateProjectAsync(model, userId));
        this.repositoryMock.Verify(repo => repo.AddAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task CreateProjectAsync_EmptyOwnerId_ThrowsValidationException()
    {
        // Arrange
        var model = CreateValidModel(Guid.Empty);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateProjectAsync(model, Guid.Empty));
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
        Assert.NotNull(result);
        var list = result.ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Проєкт 1", list[0].Name);
    }

    
    // ==========================================
    // 3. UPDATE TESTS
    // ==========================================

    [Fact]
    public async Task UpdateProjectAsync_ValidModel_CallsRepositoryUpdate()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var validModel = new UpdateProjectModel
        {
            Id = projectId,
            Name = "Оновлена назва"
        };

        var existingEntity = new Project
        {
            Id = projectId,
            Name = "Стара назва",
            OwnerId = Guid.NewGuid()
        };

        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(projectId))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();

        // Act
        await service.UpdateProjectAsync(validModel);

        // Assert
        this.repositoryMock.Verify(repo => repo.UpdateAsync(It.Is<Project>(p => p.Id == projectId && p.Name == "Оновлена назва")), Times.Once);
    }

    [Fact]
    public async Task UpdateProjectAsync_EmptyName_ThrowsValidationException()
    {
        // Arrange
        var invalidModel = new UpdateProjectModel { Id = Guid.NewGuid(), Name = string.Empty };
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateProjectAsync(invalidModel));
        this.repositoryMock.Verify(repo => repo.UpdateAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProjectAsync_ProjectNotFound_ThrowsException()
    {
        // Arrange
        var model = new UpdateProjectModel { Id = Guid.NewGuid(), Name = "Valid Name" };
        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(model.Id))
            .ReturnsAsync((Project?)null);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateProjectAsync(model));
    }

    // ==========================================
    // 4. DELETE TESTS
    // ==========================================

    [Fact]
    public async Task DeleteProjectAsync_ValidId_CallsRepositoryDelete()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var existingEntity = new Project { Id = projectId, Name = "Проєкт для видалення" };

        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(projectId))
            .ReturnsAsync(existingEntity);

        var service = this.CreateService();

        // Act
        await service.DeleteProjectAsync(projectId);

        // Assert
        this.repositoryMock.Verify(repo => repo.DeleteAsync(projectId), Times.Once);
    }

    [Fact]
    public async Task DeleteProjectAsync_ProjectNotFound_ThrowsException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        this.repositoryMock
            .Setup(repo => repo.GetByIdAsync(projectId))
            .ReturnsAsync((Project?)null);
        var service = this.CreateService();

        // Act and Assert
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteProjectAsync(projectId));
        this.repositoryMock.Verify(repo => repo.DeleteAsync(It.IsAny<Guid>()), Times.Never);
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