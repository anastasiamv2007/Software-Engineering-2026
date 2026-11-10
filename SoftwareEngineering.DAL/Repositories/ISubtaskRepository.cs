using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SoftwareEngineering.DAL.Entities;

namespace SoftwareEngineering.DAL.Repositories
{
    public interface ISubtaskRepository
    {
        Task<SubtaskEntity> AddAsync(SubtaskEntity entity);
        Task UpdateStatusAsync(Guid id, bool isCompleted);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<SubtaskEntity>> GetByTaskIdAsync(Guid taskId);
    }
}