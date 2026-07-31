using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Shipping.Services;
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
    private readonly IShippingService _shipping;
    private readonly IWebHostEnvironment _env;

    public DevDeliveriesController(IOrderService orders, IShippingService shipping, IWebHostEnvironment env)
    {
        _orders = orders;
        _shipping = shipping;
        _env = env;
    }

    /// <summary>
    /// Giả lập nhà vận chuyển báo <b>GIAO THÀNH CÔNG</b>. Đi qua pipeline webhook thật
    /// (<c>ProcessWebhookAsync</c>): lưu webhook thô, guard transition, set <c>DeliveredAt</c>,
    /// progress log nguồn Carrier, rollup order → Completed, notification cho khách.
    /// Tự đi qua bước bắc cầu (<c>Preparing → Shipped → Delivered</c>) nên gọi thẳng là được.
    /// </summary>
    [HttpPost("{deliveryId:guid}/shipping/delivered")]
    public async Task<IActionResult> SimulateDelivered(Guid deliveryId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _shipping.SimulateCarrierStatusAsync(deliveryId, DeliveryStatus.Delivered, ct));
    }

    /// <summary>
    /// Giả lập nhà vận chuyển báo <b>GIAO THẤT BẠI</b> → delivery sang <c>DeliveryFailed</c>.
    /// Sau bước này có thể test tiếp <c>POST /api/shipping/deliveries/{id}/redeliver</c> (gọi GHN thật).
    /// Tự đi qua bước bắc cầu như endpoint <c>delivered</c>.
    /// </summary>
    [HttpPost("{deliveryId:guid}/shipping/delivery-failed")]
    public async Task<IActionResult> SimulateDeliveryFailed(Guid deliveryId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _shipping.SimulateCarrierStatusAsync(deliveryId, DeliveryStatus.DeliveryFailed, ct));
    }

    /// <summary>
    /// Giả lập nhà vận chuyển đã lấy hàng và đang giao (<c>Preparing → Shipped</c>) — bước bắc cầu
    /// để tới được hai trạng thái kết thúc ở trên.
    /// </summary>
    [HttpPost("{deliveryId:guid}/shipping/delivering")]
    public async Task<IActionResult> SimulateDelivering(Guid deliveryId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _shipping.SimulateCarrierStatusAsync(deliveryId, DeliveryStatus.Shipped, ct));
    }

    /// <summary>
    /// Liệt kê toàn bộ delivery của một order (đơn nhiều store → nhiều delivery) để lấy
    /// <c>deliveryId</c>. KHÔNG lọc theo chủ đơn nên không phải đổi JWT giữa các đơn test.
    /// </summary>
    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _shipping.GetDeliveriesByOrderAsync(orderId, ct));
    }

    /// <summary>
    /// Giả lập <b>GIAO THÀNH CÔNG TOÀN BỘ</b> delivery của một order (đơn nhiều store chuyển hết
    /// trong một lần gọi). Tự đi qua các bước bắc cầu nên không cần gọi <c>/delivering</c> trước.
    /// Trả kết quả từng delivery. Order rollup sang <c>Completed</c> khi tất cả đã Delivered.
    /// </summary>
    [HttpPost("orders/{orderId:guid}/shipping/delivered")]
    public async Task<IActionResult> SimulateOrderDelivered(Guid orderId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _shipping.SimulateCarrierStatusForOrderAsync(orderId, DeliveryStatus.Delivered, ct));
    }

    /// <summary>Giả lập <b>GIAO THẤT BẠI</b> cho toàn bộ delivery của một order.</summary>
    [HttpPost("orders/{orderId:guid}/shipping/delivery-failed")]
    public async Task<IActionResult> SimulateOrderDeliveryFailed(Guid orderId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _shipping.SimulateCarrierStatusForOrderAsync(orderId, DeliveryStatus.DeliveryFailed, ct));
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
