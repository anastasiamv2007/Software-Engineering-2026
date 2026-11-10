using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class SubtaskRepository : ISubtaskRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public SubtaskRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Subtask>> GetByTaskIdAsync(Guid taskId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Subtasks
            .AsNoTracking()
            .Where(subtask => subtask.TaskId == taskId)
            .OrderBy(subtask => subtask.Title)
            .ToListAsync();
    }

    public async Task<Subtask?> GetByIdAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Subtasks
            .AsNoTracking()
            .FirstOrDefaultAsync(subtask => subtask.Id == id);
    }

    public async Task AddAsync(Subtask subtask)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Subtasks.Add(subtask);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Subtask subtask)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Entry(subtask).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        var subtask = await context.Subtasks.FindAsync(id);
        if (subtask != null)
        {
            context.Subtasks.Remove(subtask);
            await context.SaveChangesAsync();
        }
    }
}
