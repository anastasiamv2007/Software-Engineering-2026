using SoftwareEngineering.BLL.Models;
using SoftwareEngineering.BLL.Results;

namespace SoftwareEngineering.BLL.Interfaces;

public interface IReviewService
{
    Task<Result> SubmitForReviewAsync(Guid taskId, Guid currentUserId);

    Task<Result> ApproveAsync(Guid taskId, Guid currentUserId);

    Task<Result> RequestRevisionAsync(Guid taskId, string comment, Guid currentUserId);

    Task<Result<IReadOnlyList<ReviewTaskItemModel>>> GetReviewQueueAsync(Guid currentUserId);
}