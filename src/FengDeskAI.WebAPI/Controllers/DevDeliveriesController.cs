using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// [CHỈ MÔI TRƯỜNG DEVELOPMENT] Ép delivery về trạng thái Delivered để test các flow
/// phụ thuộc "đã giao" (đặt vật phẩm vào workspace, review, đổi trả…).
/// Đi qua CHÍNH OrderService.UpdateDeliveryStatusAsync theo từng bước chuyển hợp lệ
/// (Pending→Confirmed→Shipped→Delivered) nên vẫn có đủ side effect thật:
/// DeliveredAt, progress log, rollup trạng thái order (Completed), notification.
/// Ngoài Development trả 404.
/// </summary>
[Route("api/dev/deliveries")]
[Authorize]
public sealed class DevDeliveriesController : ApiControllerBase
{
    /// <summary>Chuỗi bước tiến tới Delivered — bước không hợp lệ với trạng thái hiện tại sẽ bị bỏ qua.</summary>
    private static readonly DeliveryStatus[] StepsToDelivered =
    {
        DeliveryStatus.Confirmed,
        DeliveryStatus.Shipped,
        DeliveryStatus.Delivered,
    };

    private readonly IOrderService _orders;
    private readonly IWebHostEnvironment _env;

    public DevDeliveriesController(IOrderService orders, IWebHostEnvironment env)
    {
        _orders = orders;
        _env = env;
    }

    /// <summary>Ép 1 delivery sang Delivered (đi qua các bước chuyển hợp lệ).</summary>
    [HttpPost("{deliveryId:guid}/delivered")]
    public async Task<IActionResult> ForceDelivered(Guid deliveryId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return await ForceOneAsync(deliveryId, ct);
    }

    /// <summary>Ép TẤT CẢ delivery của một order sang Delivered → order rollup thành Completed.</summary>
    [HttpPost("orders/{orderId:guid}/delivered")]
    public async Task<IActionResult> ForceOrderDelivered(Guid orderId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var order = await _orders.GetByIdAsync(orderId, CurrentUserId, isPrivileged: true, ct);
        if (!order.IsSuccess || order.Data is null)
            return NotFound(new { error = order.Message ?? "Order không tồn tại." });

        // Store chưa "tạo đơn gửi" (hoặc tạo đơn gửi đang lỗi) → order chưa có delivery nào.
        // Ép tạo delivery (Pending, không gọi nhà vận chuyển) rồi reload để có delivery id.
        if (order.Data.Deliveries.Count == 0)
        {
            var ensured = await _orders.EnsureDeliveriesAsync(orderId, ct);
            if (!ensured.IsSuccess)
                return BadRequest(new { error = ensured.Message ?? "Không tạo được delivery cho order." });

            order = await _orders.GetByIdAsync(orderId, CurrentUserId, isPrivileged: true, ct);
            if (!order.IsSuccess || order.Data is null)
                return NotFound(new { error = order.Message ?? "Order không tồn tại." });
        }

        var results = new List<object>();
        foreach (var delivery in order.Data.Deliveries)
        {
            var res = await ForceOneCoreAsync(delivery.Id, ct);
            results.Add(new { deliveryId = delivery.Id, res.status, res.message });
        }

        return Ok(new { orderId, deliveries = results });
    }

    private async Task<IActionResult> ForceOneAsync(Guid deliveryId, CancellationToken ct)
    {
        var (status, message) = await ForceOneCoreAsync(deliveryId, ct);
        return status is null
            ? NotFound(new { error = message })
            : Ok(new { deliveryId, status, message });
    }

    /// <summary>
    /// Đẩy delivery qua từng bước tới Delivered. Bước fail vì transition không hợp lệ
    /// (đã qua giai đoạn đó) thì bỏ qua; chỉ coi là lỗi nếu bước cuối (Delivered) không thành công
    /// mà delivery cũng chưa ở Delivered (vd: Cancelled/Returned không ép được).
    /// </summary>
    private async Task<(string? status, string? message)> ForceOneCoreAsync(Guid deliveryId, CancellationToken ct)
    {
        DeliveryResponse? last = null;
        string? lastError = null;

        foreach (var step in StepsToDelivered)
        {
            var res = await _orders.UpdateDeliveryStatusAsync(deliveryId, CurrentUserId, isAdmin: true,
                new UpdateDeliveryStatusRequest { Status = step, Note = "[DEV] force delivered" }, ct);

            if (res.IsSuccess && res.Data is not null) last = res.Data;
            else lastError = res.Message;

            if (res.StatusCode == StatusCodes.Status404NotFound)
                return (null, res.Message ?? "Delivery không tồn tại.");
        }

        if (last?.Status == DeliveryStatus.Delivered)
            return (last.Status.ToString(), "Đã ép delivery sang Delivered.");

        // Không bước nào thành công — có thể đã Delivered từ trước, hoặc Cancelled/Returned.
        return (last?.Status.ToString() ?? "Unchanged",
            $"Không ép được sang Delivered: {lastError ?? "trạng thái hiện tại không cho phép."}");
    }
}
