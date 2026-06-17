using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public interface IRecommendationService
{
    /// <summary>Chấm điểm + gọi AI diễn giải cho một workspace của user, lưu lại phiên gợi ý.</summary>
    Task<IServiceResult<RecommendationResponse>> GenerateAsync(
        Guid userId, GenerateRecommendationRequest request, CancellationToken ct = default);

    /// <summary>Lấy lại một phiên gợi ý đã lưu (theo chủ sở hữu).</summary>
    Task<IServiceResult<RecommendationResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
}
