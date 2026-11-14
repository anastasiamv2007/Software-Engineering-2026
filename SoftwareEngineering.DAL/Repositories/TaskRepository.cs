using Microsoft.EntityFrameworkCore;
using Npgsql;
using SoftwareEngineering.DAL.Exceptions;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public TaskRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    private const string TaskCodeIndexName = "ix_tasks_project_id_task_code";

    public async Task<IReadOnlyList<string>> GetTaskCodesAsync(Guid projectId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .Select(task => task.TaskCode)
            .ToListAsync();
    }

    public async Task AddAsync(TaskItem task)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Tasks.Add(task);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TaskCodeIndexName })
        {
            throw new DuplicateTaskCodeException(task.TaskCode, ex);
        }
    }

    public async Task<IEnumerable<TaskItem>> GetAsync()
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks.AsNoTracking().Include(task => task.Category).ToListAsync();
    }

    public async Task<IReadOnlyList<TaskItem>> GetByUserAsync(Guid userId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks
            .AsNoTracking()
            .Include(task => task.Category)
            .Where(task => task.Project.OwnerId == userId || task.Project.Members.Any(member => member.UserId == userId))
            .ToListAsync();
    }

    public async Task<TaskItem?> GetDetailsAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Tasks
            .AsNoTracking()
            .AsSplitQuery()
            .Include(task => task.Project).ThenInclude(project => project.Owner)
            .Include(task => task.Project).ThenInclude(project => project.Members).ThenInclude(member => member.User)
            .Include(task => task.Owner)
            .Include(task => task.Assignee)
            .Include(task => task.Category)
            .Include(task => task.Subtasks)
            .Include(task => task.Comments)
            .FirstOrDefaultAsync(task => task.Id == id);
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
