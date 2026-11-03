using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync();

    Task<Category?> GetByIdAsync(int id);

    Task<Category?> FindByNameAsync(string name);

    Task AddAsync(Category category);

    Task UpdateAsync(Category category);

    Task DeleteAsync(int id);
}