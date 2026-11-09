using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public CommentRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(Comment comment)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        await context.Comments.AddAsync(comment);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Comment>> GetByTaskIdAsync(Guid taskId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Comments
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }
    public async Task<IEnumerable<Comment>> GetByTaskIdWithAuthorInfoAsync(Guid taskId, Guid projectId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var query = from c in context.Comments
                    where c.TaskId == taskId
                    join u in context.Users on c.AuthorId equals u.Id
                    join m in context.ProjectMembers
                        on new { ProjectId = projectId, UserId = u.Id }
                        equals new { m.ProjectId, m.UserId } into memberGroup
                    from member in memberGroup.DefaultIfEmpty()
                    orderby c.CreatedAt ascending
                    select new Comment
                    {
                        Id = c.Id,
                        TaskId = c.TaskId,
                        AuthorId = c.AuthorId,
                        Content = c.Content,
                        CreatedAt = c.CreatedAt,
                        AuthorName = u.FullName ?? u.Email ?? "Користувач",
                        RoleText = member != null ? member.Role.ToString() : "Учасник"
                    };

        return await query.ToListAsync();
    }
}