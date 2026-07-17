using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Domain.Entities.Workspace;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IWorkspaceProfileRepository : IGenericRepository<WorkspaceProfile>
{
    Task<WorkspaceProfile?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<List<WorkspaceProfile>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<WorkspaceProfile?> GetDefaultByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default);

    // ===== Placement sản phẩm đã mua vào workspace =====

    /// <summary>Placements của 1 phòng, kèm OrderItem.Delivery (status) + Product (element inputs/elements) để build vector.</summary>
    Task<List<WorkspaceProductPlacement>> GetPlacementsAsync(Guid workspaceProfileId, CancellationToken ct = default);

    /// <summary>Placement hiện tại của 1 order item (tracked) — dùng để chuyển phòng/gỡ.</summary>
    Task<WorkspaceProductPlacement?> GetPlacementByOrderItemAsync(Guid orderItemId, Guid userId, CancellationToken ct = default);

    Task AddPlacementAsync(WorkspaceProductPlacement placement, CancellationToken ct = default);
    void RemovePlacement(WorkspaceProductPlacement placement);

    /// <summary>
    /// Các order item user đã mua đủ điều kiện đặt phòng (delivery không Cancelled/Returned/DeliveryFailed)
    /// + trạng thái giao + đang đặt ở workspace nào (nếu có).
    /// </summary>
    Task<List<PurchasedItemResponse>> GetPurchasedItemsAsync(Guid userId, CancellationToken ct = default);
}
