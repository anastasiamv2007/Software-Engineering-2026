using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext context;

    public TaskRepository(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<int> CountByProjectAsync(Guid projectId)
    {
        return await this.context.Tasks.CountAsync(task => task.ProjectId == projectId);
    }

    public async Task AddAsync(TaskItem task)
    {
        this.context.Tasks.Add(task);
        await this.context.SaveChangesAsync();
    }

    public async Task<IEnumerable<TaskItem>> GetAsync()
    {
        return await this.context.Tasks.ToListAsync();
    }

    // 2
    public async Task<TaskItem?> GetByIdAsync(Guid id)
    {
        return await this.context.Tasks.FirstOrDefaultAsync(task => task.Id == id);
    }

    // 3
    public async Task UpdateAsync(TaskItem task)
    {
        this.context.Tasks.Update(task);
        await this.context.SaveChangesAsync();
    }

    public async Task DeleteAsync(TaskItem entity)
    {
        this.context.Tasks.Remove(entity);
        await this.context.SaveChangesAsync();
    }

    // 5
    public async Task DeleteAsync(Guid id)
    {
        var task = await this.context.Tasks.FindAsync(id);
        if (task != null)
        {
            this.context.Tasks.Remove(task);
            await this.context.SaveChangesAsync();
        }
    }

    // Новий метод для фільтрації завдань за проєктом
    public async Task<IEnumerable<TaskItem>> GetTasksByProjectIdAsync(Guid projectId)
    {
        return await this.context.Tasks
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();
    }
}