using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext context;

    public ProjectRepository(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id)
    {
        return await this.context.Projects
            .Include(p => p.Owner)
            .Include(p => p.Members)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Project>> GetAllByUserIdAsync(Guid userId)
    {
        return await this.context.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == userId || p.Members.Any(m => m.UserId == userId))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> FindByNameAsync(string name)
    {
        var lowerName = name.ToLower();

        return await this.context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(project => project.Name.ToLower() == lowerName);
    }

    public async Task<Guid?> GetDefaultOwnerIdAsync()
    {
        return await this.context.Users
            .OrderBy(user => user.CreatedAt)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(Project project)
    {
        this.context.Projects.Add(project);
        await this.context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        this.context.Projects.Update(project);
        await this.context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await this.context.Projects.FindAsync(id);
        if (project != null)
        {
            this.context.Projects.Remove(project);
            await this.context.SaveChangesAsync();
        }
    }
}