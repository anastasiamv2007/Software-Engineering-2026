using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public TaskRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<int> CountByProjectAsync(Guid projectId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks.CountAsync(task => task.ProjectId == projectId);
    }

    public async Task AddAsync(TaskItem task)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<TaskItem>> GetAsync()
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks.AsNoTracking().ToListAsync();
    }

    // 2
    public async Task<TaskItem?> GetByIdAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks.AsNoTracking().FirstOrDefaultAsync(task => task.Id == id);
    }

    // 3
    public async Task UpdateAsync(TaskItem task)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();

        // Сутність прийшла з іншого контексту — позначаємо змінною лише її, без навігацій
        context.Entry(task).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(TaskItem entity)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Entry(entity).State = EntityState.Deleted;
        await context.SaveChangesAsync();
    }

    // 5
    public async Task DeleteAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        var task = await context.Tasks.FindAsync(id);
        if (task != null)
        {
            context.Tasks.Remove(task);
            await context.SaveChangesAsync();
        }
    }
}
