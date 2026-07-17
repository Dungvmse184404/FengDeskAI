using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class WorkspaceProfileRepository : GenericRepository<WorkspaceProfile>, IWorkspaceProfileRepository
{
    public WorkspaceProfileRepository(AppDbContext context) : base(context) { }

    public Task<WorkspaceProfile?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);

    public Task<List<WorkspaceProfile>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.Where(w => w.UserId == userId)
               .OrderByDescending(w => w.IsDefault)
               .ThenByDescending(w => w.UpdatedAt)
               .ToListAsync(ct);

    public Task<WorkspaceProfile?> GetDefaultByUserIdAsync(Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(w => w.UserId == userId && w.IsDefault, ct);

    public Task ClearDefaultsForUserAsync(Guid userId, CancellationToken ct = default)
        => _set.Where(w => w.UserId == userId && w.IsDefault)
               .ExecuteUpdateAsync(s => s.SetProperty(w => w.IsDefault, false), ct);

    // ===== Placement sản phẩm đã mua vào workspace =====

    public Task<List<WorkspaceProductPlacement>> GetPlacementsAsync(Guid workspaceProfileId, CancellationToken ct = default)
        => _context.Set<WorkspaceProductPlacement>()
            .AsNoTracking()
            .Where(p => p.WorkspaceProfileId == workspaceProfileId)
            .Include(p => p.OrderItem).ThenInclude(i => i.Delivery)
            .Include(p => p.Product).ThenInclude(pr => pr.Elements)
            .Include(p => p.Product).ThenInclude(pr => pr.Images)
            .OrderBy(p => p.PlacedAt)
            .ToListAsync(ct);

    public Task<WorkspaceProductPlacement?> GetPlacementByOrderItemAsync(Guid orderItemId, Guid userId, CancellationToken ct = default)
        => _context.Set<WorkspaceProductPlacement>()
            .FirstOrDefaultAsync(p => p.OrderItemId == orderItemId && p.UserId == userId, ct);

    public async Task AddPlacementAsync(WorkspaceProductPlacement placement, CancellationToken ct = default)
        => await _context.Set<WorkspaceProductPlacement>().AddAsync(placement, ct);

    public void RemovePlacement(WorkspaceProductPlacement placement)
        => _context.Set<WorkspaceProductPlacement>().Remove(placement);

    public async Task<List<PurchasedItemResponse>> GetPurchasedItemsAsync(Guid userId, CancellationToken ct = default)
    {
        // LẤY THEO ORDER (không bắt buộc đã có delivery): COD / đơn mới chưa tạo vận đơn vẫn
        // xem trước được tác động lên radar. Chỉ nhận đơn đã Paid (online) hoặc đã vào
        // Processing/Completed (COD store đã xác nhận / đang giao / đã xong).
        // Nếu ĐÃ có delivery thì loại delivery bị hủy/hoàn/giao thất bại.
        var okOrderStatuses = new[]
        {
            Domain.Enums.Sales.OrderStatus.Paid,
            Domain.Enums.Sales.OrderStatus.Processing,
            Domain.Enums.Sales.OrderStatus.Completed,
        };
        var excludedDelivery = new[]
        {
            Domain.Enums.Sales.DeliveryStatus.Cancelled,
            Domain.Enums.Sales.DeliveryStatus.Returned,
            Domain.Enums.Sales.DeliveryStatus.DeliveryFailed,
        };

        var items = await _context.Set<Domain.Entities.Sales.OrderItem>()
            .AsNoTracking()
            .Where(i => i.Order.CustomerId == userId
                        && okOrderStatuses.Contains(i.Order.Status)
                        && (i.Delivery == null || !excludedDelivery.Contains(i.Delivery!.Status)))
            .Select(i => new
            {
                i.Id,
                i.ProductName,
                i.Quantity,
                Status = i.Delivery != null ? (Domain.Enums.Sales.DeliveryStatus?)i.Delivery!.Status : null,
                ProductId = i.ProductItem.ProductId,
                Image = i.ProductItem.Product.Images
                    .OrderBy(img => img.SortOrder)
                    .Select(img => img.Url)
                    .FirstOrDefault(),
            })
            .OrderByDescending(i => i.Id)
            .ToListAsync(ct);

        var itemIds = items.Select(i => i.Id).ToList();
        var placements = await _context.Set<WorkspaceProductPlacement>()
            .AsNoTracking()
            .Where(p => itemIds.Contains(p.OrderItemId))
            .Select(p => new { p.OrderItemId, p.WorkspaceProfileId, WorkspaceName = p.WorkspaceProfile.Name })
            .ToListAsync(ct);
        var placedBy = placements.ToDictionary(p => p.OrderItemId);

        return items.Select(i => new PurchasedItemResponse
        {
            OrderItemId = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            ProductImage = i.Image,
            Quantity = i.Quantity,
            DeliveryStatus = (i.Status ?? Domain.Enums.Sales.DeliveryStatus.Pending).ToString(),
            IsDelivered = i.Status == Domain.Enums.Sales.DeliveryStatus.Delivered,
            PlacedWorkspaceProfileId = placedBy.TryGetValue(i.Id, out var p) ? p.WorkspaceProfileId : null,
            PlacedWorkspaceName = placedBy.TryGetValue(i.Id, out var p2) ? p2.WorkspaceName : null,
        }).ToList();
    }
}
