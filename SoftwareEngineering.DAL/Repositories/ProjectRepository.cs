using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;
using SoftwareEngineering.DAL.Enums;

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

    public async Task AddAsync(Project project)
    {
        this.context.Projects.Add(project);
        await this.context.SaveChangesAsync();
    }

    public async Task<ProjectRole?> GetMemberRoleAsync(Guid projectId, Guid userId)
    {
        var isProjectOwner = await this.context.Projects
            .AnyAsync(project => project.Id == projectId && project.OwnerId == userId);
        if (isProjectOwner)
        {
            return ProjectRole.Owner;
        }

        return await this.context.ProjectMembers
            .Where(member => member.ProjectId == projectId && member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .FirstOrDefaultAsync();
    }
}