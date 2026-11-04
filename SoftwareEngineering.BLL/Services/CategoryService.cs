using SoftwareEngineering.BLL.Interfaces;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Repositories;
using System.Text.RegularExpressions;

namespace SoftwareEngineering.BLL.Services;

public class CategoryService : BaseService, ICategoryService
{
    private const int MaxNameLength = 100;
    private const string UnexpectedErrorMessage = "Сталася внутрішня помилка. Спробуйте ще раз.";

    private static readonly Regex ColorRegex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly ICategoryRepository categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        this.categoryRepository = categoryRepository;
    }

    public async Task<Result<IReadOnlyList<CategoryModel>>> GetAllAsync()
    {
        return await this.ExecuteAsync("Отримання категорій", async () =>
        {
            var categories = await this.categoryRepository.GetAllAsync();
            IReadOnlyList<CategoryModel> models = categories.Select(ToModel).ToList();

            return Result.Ok(models);
        });
    }

    public async Task<Result<CategoryModel>> CreateAsync(CreateCategoryModel model)
    {
        return await this.ExecuteAsync("Створення категорії", async () =>
        {
            if (model is null)
            {
                return Result.Fail<CategoryModel>(ErrorType.Validation, "Дані категорії не передано.");
            }

            var validation = Validate(model.Name, model.ColorHex);
            if (validation is not null)
            {
                return Result.Fail<CategoryModel>(ErrorType.Validation, validation);
            }

            var name = model.Name.Trim();
            var existing = await this.categoryRepository.FindByNameAsync(name);
            if (existing is not null)
            {
                return Result.Fail<CategoryModel>(ErrorType.Duplicate, "Категорія з такою назвою вже існує.");
            }

            var category = new Category { Name = name, ColorHex = model.ColorHex.ToUpperInvariant() };
            await this.categoryRepository.AddAsync(category);

            this.Logger.Information("Створено категорію {CategoryName} ({CategoryId}) з кольором {ColorHex}", category.Name, category.Id, category.ColorHex);

            return Result.Ok(ToModel(category));
        });
    }

    public async Task<Result<CategoryModel>> UpdateAsync(UpdateCategoryModel model)
    {
        return await this.ExecuteAsync("Редагування категорії", async () =>
        {
            if (model is null)
            {
                return Result.Fail<CategoryModel>(ErrorType.Validation, "Дані категорії не передано.");
            }

            var validation = Validate(model.Name, model.ColorHex);
            if (validation is not null)
            {
                return Result.Fail<CategoryModel>(ErrorType.Validation, validation);
            }

            var category = await this.categoryRepository.GetByIdAsync(model.Id);
            if (category is null)
            {
                return Result.Fail<CategoryModel>(ErrorType.NotFound, "Категорію не знайдено.");
            }

            var name = model.Name.Trim();
            var sameName = await this.categoryRepository.FindByNameAsync(name);
            if (sameName is not null && sameName.Id != model.Id)
            {
                return Result.Fail<CategoryModel>(ErrorType.Duplicate, "Категорія з такою назвою вже існує.");
            }

            category.Name = name;
            category.ColorHex = model.ColorHex.ToUpperInvariant();
            await this.categoryRepository.UpdateAsync(category);

            this.Logger.Information("Оновлено категорію {CategoryId}: назва {CategoryName}, колір {ColorHex}", category.Id, category.Name, category.ColorHex);

            return Result.Ok(ToModel(category));
        });
    }

    public async Task<Result> DeleteAsync(int id)
    {
        return await this.ExecuteAsync("Видалення категорії", async () =>
        {
            var category = await this.categoryRepository.GetByIdAsync(id);
            if (category is null)
            {
                return Result.Fail(ErrorType.NotFound, "Категорію не знайдено.");
            }

            await this.categoryRepository.DeleteAsync(id);

            this.Logger.Information("Видалено категорію {CategoryId} ({CategoryName}); у пов'язаних завданнях категорію скинуто", id, category.Name);

            return Result.Ok();
        });
    }

    private static string? Validate(string? name, string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Назва категорії не може бути порожньою.";
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return $"Назва категорії не може перевищувати {MaxNameLength} символів.";
        }

        if (string.IsNullOrWhiteSpace(colorHex) || !ColorRegex.IsMatch(colorHex))
        {
            return "Колір має бути у форматі #RRGGBB.";
        }

        return null;
    }

    private static CategoryModel ToModel(Category category)
    {
        return new CategoryModel
        {
            Id = category.Id,
            Name = category.Name,
            ColorHex = category.ColorHex,
            TasksCount = category.Tasks.Count,
        };
    }

    private async Task<Result<T>> ExecuteAsync<T>(string actionName, Func<Task<Result<T>>> action)
    {
        try
        {
            this.Logger.Information("Початок виконання дії: {ActionName}", actionName);
            var result = await action();
            this.LogOutcome(actionName, result);

            return result;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час виконання дії: {ActionName}", actionName);

            return Result.Fail<T>(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    private async Task<Result> ExecuteAsync(string actionName, Func<Task<Result>> action)
    {
        try
        {
            this.Logger.Information("Початок виконання дії: {ActionName}", actionName);
            var result = await action();
            this.LogOutcome(actionName, result);

            return result;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Критична помилка під час виконання дії: {ActionName}", actionName);

            return Result.Fail(ErrorType.Unexpected, UnexpectedErrorMessage);
        }
    }

    private void LogOutcome(string actionName, Result result)
    {
        if (result.IsFailure)
        {
            this.Logger.Warning("Дію {ActionName} відхилено ({ErrorType}): {ErrorMessage}", actionName, result.Error!.Type, result.Error.Message);
        }
        else
        {
            this.Logger.Information("Успішно виконано дію: {ActionName}", actionName);
        }
    }
}