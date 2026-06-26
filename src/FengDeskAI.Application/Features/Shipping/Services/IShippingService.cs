using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Shipping.DTOs;

namespace FengDeskAI.Application.Features.Shipping.Services;

public interface IShippingService
{
    /// <summary>Tiếp nhận callback nhà vận chuyển: lưu webhook thô, cập nhật delivery + ghi progress log.</summary>
    Task<IServiceResult> ProcessWebhookAsync(ShippingWebhookRequest request, CancellationToken ct = default);

    /// <summary>Lịch sử tiến trình của một delivery (owner/staff store hoặc admin).</summary>
    Task<IServiceResult<List<DeliveryProgressLogResponse>>> GetProgressLogsAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Yêu cầu nhà vận chuyển giao lại một delivery đang ở trạng thái giao thất bại (owner/staff store hoặc admin).
    /// Không tự đổi trạng thái — webhook tiếp theo sẽ cập nhật. Xem Documents/GHN_INTEGRATION.md §9.2.
    /// </summary>
    Task<IServiceResult> RedeliverAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
