using FengDeskAI.Domain.Entities.CustomerCare;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IRecommendationRepository : IGenericRepository<Recommendation>
{
    /// <summary>Phiên gợi ý kèm Items, giới hạn theo chủ sở hữu.</summary>
    Task<Recommendation?> GetDetailForUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Toàn bộ luật ngũ hành (25 dòng) để dựng bảng điểm element.</summary>
    Task<List<FengShuiRule>> GetAllRulesAsync(CancellationToken ct = default);
}
