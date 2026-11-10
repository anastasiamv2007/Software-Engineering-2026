using System;
using System.Threading.Tasks;
using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces
{
    public interface ISubtaskService
    {
        Task<Result<SubtaskModel>> AddAsync(Guid taskId, string title);
        Task<Result<bool>> ToggleAsync(Guid id, bool currentStatus);
        Task<Result<bool>> DeleteAsync(Guid id);
    }
}