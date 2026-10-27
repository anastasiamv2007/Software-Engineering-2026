using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;

namespace SoftwareEngineering.DAL.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public ProjectRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<Project?> GetByIdAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Projects
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Project>> GetAllByUserIdAsync(Guid userId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Projects
            .AsNoTracking()
            .Include(p => p.Tasks)
            .Where(p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> FindByNameAsync(string name)
    {
        var lowerName = name.ToLower();

        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(project => project.Name.ToLower() == lowerName);
    }

    public async Task AddAsync(Project project)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Projects.Add(project);
        await context.SaveChangesAsync();
    }

    public async Task<ProjectRole?> GetMemberRoleAsync(Guid projectId, Guid userId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();

        var isProjectOwner = await context.Projects
            .AnyAsync(project => project.Id == projectId && project.OwnerId == userId);
        if (isProjectOwner)
        {
            return ProjectRole.Owner;
        }

        return await context.ProjectMembers
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();

        // Оновлюємо лише сам проєкт: підвантажені Owner/Members/Tasks не чіпаємо
        context.Entry(project).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        var project = await context.Projects.FindAsync(id);
        if (project != null)
        {
            context.Projects.Remove(project);
            await context.SaveChangesAsync();
        }
    }
}
