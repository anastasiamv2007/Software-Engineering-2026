using Moq;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.BLL.Services;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;

namespace SoftwareEngineering.Tests;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> repositoryMock = new();

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsSuccessAndSavesCategory()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(new CreateCategoryModel { Name = " Навчання ", ColorHex = "#3b82f6" });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Навчання", result.Value!.Name);
        this.repositoryMock.Verify(
            repository => repository.AddAsync(It.Is<Category>(c => c.Name == "Навчання" && c.ColorHex == "#3B82F6")),
            Times.Once);
    }

    [Theory]
    [InlineData("", "#3B82F6")]
    [InlineData("   ", "#3B82F6")]
    [InlineData("Навчання", "red")]
    [InlineData("Навчання", "#12345")]
    public async Task CreateAsync_InvalidNameOrColor_ReturnsValidationErrorAndDoesNotSave(string name, string color)
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(new CreateCategoryModel { Name = name, ColorHex = color });

        // Assert
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        this.repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Category>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameIgnoringCase_ReturnsDuplicateError()
    {
        // Arrange
        this.repositoryMock
            .Setup(repository => repository.FindByNameAsync("Навчання"))
            .ReturnsAsync(new Category { Id = 1, Name = "навчання", ColorHex = "#000000" });
        var service = this.CreateService();

        // Act
        var result = await service.CreateAsync(new CreateCategoryModel { Name = "Навчання", ColorHex = "#3B82F6" });

        // Assert
        Assert.Equal(ErrorType.Duplicate, result.Error!.Type);
    }

    [Fact]
    public async Task DeleteAsync_CategoryNotFound_ReturnsNotFoundAndDoesNotDelete()
    {
        // Arrange
        var service = this.CreateService();

        // Act
        var result = await service.DeleteAsync(99);

        // Assert
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        this.repositoryMock.Verify(repository => repository.DeleteAsync(It.IsAny<int>()), Times.Never);
    }

    private CategoryService CreateService()
    {
        return new CategoryService(this.repositoryMock.Object);
    }
}