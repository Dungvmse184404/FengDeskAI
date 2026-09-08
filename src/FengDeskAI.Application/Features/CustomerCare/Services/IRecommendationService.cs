using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

public interface IRecommendationService
{
    /// <summary>Chấm điểm + gọi AI diễn giải cho một workspace của user, lưu lại phiên gợi ý.</summary>
    Task<IServiceResult<RecommendationResponse>> GenerateAsync(
        Guid userId, GenerateRecommendationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gợi ý vật phẩm MANG THEO NGƯỜI (đeo tay, mặt dây, để ví, treo xe) — chấm theo bản mệnh/dụng thần
    /// của user, không cần workspace. Không gọi AI microservice (xem ADR product-placement §7).
    /// </summary>
    Task<IServiceResult<RecommendationResponse>> GeneratePersonalAsync(
        Guid userId, GeneratePersonalRecommendationRequest request, CancellationToken ct = default);

    /// <summary>Lấy lại một phiên gợi ý đã lưu (theo chủ sở hữu).</summary>
    Task<IServiceResult<RecommendationResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Độ phù hợp của 1 sản phẩm × 1 workspace — không loại sản phẩm, cho trang chi tiết sản phẩm.</summary>
    Task<IServiceResult<ProductFitResponse>> GetProductFitAsync(
        Guid productId, Guid workspaceProfileId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Độ phù hợp của 1 sản phẩm với BẢN MỆNH user — không cần workspace (v3.2 §10.6 · R3).
    /// Dành cho trang chi tiết vật phẩm <see cref="Domain.Enums.Catalog.ProductPlacement.Carry"/>, nơi
    /// <c>GetProductFitAsync</c> không dùng được vì nó bắt buộc <c>workspaceProfileId</c> và chấm theo gap
    /// của phòng — sai bản chất với vật đeo trên người.
    /// <para>Giữ hợp đồng "fit luôn có kết quả": không loại sản phẩm, xung khắc chỉ vào điểm + caution.</para>
    /// </summary>
    Task<IServiceResult<PersonalFitResponse>> GetPersonalFitAsync(
        Guid productId, Guid userId, CancellationToken ct = default);
}
