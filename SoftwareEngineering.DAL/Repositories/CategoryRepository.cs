using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public CategoryRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync()
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Categories
            .AsNoTracking()
            .Include(category => category.Tasks)
            .OrderBy(category => category.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.Id == id);
    }

    public async Task<Category?> FindByNameAsync(string name)
    {
        var lowerName = name.ToLower();

        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(category => category.Name.ToLower() == lowerName);
    }

    public async Task AddAsync(Category category)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Categories.Add(category);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Category category)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Entry(category).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        var category = await context.Categories.FindAsync(id);
        if (category != null)
        {
            // FK tasks.category_id має ON DELETE SET NULL, тож у завданнях категорія скинеться в null
            context.Categories.Remove(category);
            await context.SaveChangesAsync();
        }
    }
}