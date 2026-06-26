using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;

namespace FengDeskAI.Application.Features.Sales.Services;

public interface IOrderService
{
    Task<IServiceResult<OrderDetailResponse>> CheckoutAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default);

    /// <summary>Xem trước phí ship cho FE (cùng input checkout: địa chỉ + items/giỏ) — không tạo đơn.</summary>
    Task<IServiceResult<ShippingFeePreviewResponse>> PreviewShippingFeeAsync(Guid userId, CheckoutRequest request, CancellationToken ct = default);
    Task<IServiceResult<PagedResult<OrderListItemResponse>>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default);

    /// <summary>Tất cả đơn của mọi customer (paged) — chỉ admin.</summary>
    Task<IServiceResult<PagedResult<OrderListItemResponse>>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    /// <summary>Chi tiết đơn. Customer chỉ xem đơn của mình; Staff trở lên (isPrivileged) xem được mọi đơn.</summary>
    Task<IServiceResult<OrderDetailResponse>> GetByIdAsync(Guid id, Guid userId, bool isPrivileged, CancellationToken ct = default);
    Task<IServiceResult<OrderDetailResponse>> CancelAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task<IServiceResult<PagedResult<StoreDeliveryResponse>>> GetStoreDeliveriesAsync(Guid storeId, Guid userId, bool isAdmin, PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<DeliveryResponse>> UpdateDeliveryStatusAsync(Guid deliveryId, Guid userId, bool isAdmin, UpdateDeliveryStatusRequest request, CancellationToken ct = default);
}
