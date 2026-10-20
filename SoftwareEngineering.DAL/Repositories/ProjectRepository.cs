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
}