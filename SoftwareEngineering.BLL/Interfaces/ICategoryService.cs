using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public interface ICategoryService : IService
{
    Task<Result<IReadOnlyList<CategoryModel>>> GetAllAsync();

    Task<Result<CategoryModel>> CreateAsync(CreateCategoryModel model);

    Task<Result<CategoryModel>> UpdateAsync(UpdateCategoryModel model);

    Task<Result> DeleteAsync(int id);
}