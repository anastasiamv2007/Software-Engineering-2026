using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories;

public interface ICommentRepository
{
    Task AddAsync(Comment comment);
    Task<IEnumerable<Comment>> GetByTaskIdAsync(Guid taskId);
    Task<IEnumerable<Comment>> GetByTaskIdWithAuthorInfoAsync(Guid taskId, Guid projectId);
}