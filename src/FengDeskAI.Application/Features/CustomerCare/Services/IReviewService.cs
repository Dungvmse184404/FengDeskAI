
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services
{
    public interface IReviewService
    {
        Task<IServiceResult<List<ReviewResponse>>> GetAllAsync(CancellationToken ct = default);
        Task<IServiceResult<List<ReviewResponse>>> GetMyAsync(Guid userId, CancellationToken ct = default);
        Task<IServiceResult<CreateReviewRespond>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default);
        Task<IServiceResult<UpdateReviewRespond>> UpdateAsync(Guid id, Guid userId, UpdateReviewRequest request, CancellationToken ct = default);
        Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    }
}
