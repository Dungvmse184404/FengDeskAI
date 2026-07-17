using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.Workspace.Services;

public interface IWorkspaceProfileService
{
    Task<IServiceResult<List<WorkspaceProfileResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Vector ngũ hành (ideal/adjustedIdeal/current/gap) của workspace — không chạy phiên recommendation.</summary>
    Task<IServiceResult<WorkspaceElementAnalysisResponse>> GetElementAnalysisAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> GetDefaultAsync(Guid userId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> CreateAsync(Guid userId, CreateWorkspaceProfileRequest request, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> UpdateAsync(Guid id, Guid userId, UpdateWorkspaceProfileRequest request, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Từ vựng màu/vật liệu/hình khối hợp lệ — cho FE dựng tag picker "hiện trạng phòng hiện tại".</summary>
    Task<IServiceResult<ElementInputVocabularyResponse>> GetElementInputVocabularyAsync(CancellationToken ct = default);

    // ===== Đặt sản phẩm đã mua vào workspace (radar tính lúc đọc, chỉ lưu mapping) =====

    /// <summary>Sản phẩm user đã mua đủ điều kiện đặt phòng + đang đặt ở đâu.</summary>
    Task<IServiceResult<List<PurchasedItemResponse>>> GetPurchasedItemsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đặt (hoặc CHUYỂN từ phòng khác) 1 order item đã mua vào workspace.</summary>
    Task<IServiceResult> PlaceProductAsync(Guid workspaceProfileId, Guid userId, Guid orderItemId, CancellationToken ct = default);

    /// <summary>Gỡ 1 order item khỏi workspace.</summary>
    Task<IServiceResult> RemovePlacementAsync(Guid workspaceProfileId, Guid userId, Guid orderItemId, CancellationToken ct = default);
}
