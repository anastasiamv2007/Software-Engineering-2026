using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public CommentRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        this.contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Comment>> GetByTaskIdAsync(Guid taskId)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Comments
            .AsNoTracking()
            .Include(comment => comment.Author)
            .Where(comment => comment.TaskId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync();
    }

    public async Task<Comment?> GetByIdAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        return await context.Comments
            .AsNoTracking()
            .FirstOrDefaultAsync(comment => comment.Id == id);
    }

    public async Task AddAsync(Comment comment)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        context.Comments.Add(comment);
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await this.contextFactory.CreateDbContextAsync();
        var comment = await context.Comments.FindAsync(id);
        if (comment != null)
        {
            context.Comments.Remove(comment);
            await context.SaveChangesAsync();
        }
    }
}
