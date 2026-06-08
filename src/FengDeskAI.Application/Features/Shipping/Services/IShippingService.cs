using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Shipping.DTOs;

namespace FengDeskAI.Application.Features.Shipping.Services;

public interface IShippingService
{
    /// <summary>Tiếp nhận callback nhà vận chuyển: lưu webhook thô, cập nhật delivery + ghi progress log.</summary>
    Task<IServiceResult> ProcessWebhookAsync(ShippingWebhookRequest request, CancellationToken ct = default);

    /// <summary>Lịch sử tiến trình của một delivery (owner/staff store hoặc admin).</summary>
    Task<IServiceResult<List<DeliveryProgressLogResponse>>> GetProgressLogsAsync(Guid deliveryId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
