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
    public async Task<TaskItem?> GetByIdAsync(Guid id)
    {
        return await this.context.Tasks.FirstOrDefaultAsync(task => task.Id == id);
    }

    public async Task UpdateAsync(TaskItem task)
    {
        this.context.Tasks.Update(task);
        await this.context.SaveChangesAsync();
    }
}