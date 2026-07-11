using System.Text.Json;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Domain.Enums.Payment;
using Microsoft.Extensions.Caching.Memory;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Xác nhận 1 draft do <c>prepare_order</c> sinh ra: re-validate giá/tồn kho rồi mới tạo đơn thật + link
/// thanh toán. Chỉ nhận <c>draftId</c> (không nhận productId trực tiếp) nên model không thể bịa đơn.
/// Draft bị xóa khỏi cache ngay khi đọc (dùng 1 lần) — gọi lại cùng draftId luôn báo hết hạn.
/// Chỉ enable ở phòng riêng user↔AI (xem <see cref="AiToolContext.IsPrivateRoom"/>).
/// </summary>
public sealed class ConfirmOrderTool : IAiTool
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;
    private readonly IMemoryCache _cache;

    public ConfirmOrderTool(IOrderService orders, IPaymentService payments, IMemoryCache cache)
    {
        _orders = orders;
        _payments = payments;
        _cache = cache;
    }

    public string Name => "confirm_order";

    public string Description =>
        "Confirm a draft order previously created by prepare_order and place the real order. ONLY call this " +
        "after the user's NEXT message clearly agrees to the summary you already read back to them — never in " +
        "the same turn you show the summary. Never call this with a draftId you made up.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["draftId"] = new("string", "The draftId returned by prepare_order, if you still have it. " +
            "Omit it if you no longer have the exact id — the system will use the user's latest prepared draft."),
        ["paymentMethod"] = new("string", "Payment method (default PayOS). COD must be explicitly requested by the user.", Enum: new[] { "PayOS", "COD" }),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        // draftId chỉ sống trong tool result của LƯỢT prepare — tool exchange không lưu vào history nên
        // sang lượt user xác nhận, model thường không còn id. Fallback: draft MỚI NHẤT user đã prepare
        // trong phòng này (pointer do prepare_order set, cùng TTL — model không thể "bịa" draft).
        var requested = ToolArgs.GetGuid(arguments, "draftId");
        var latestKey = OrderDraftCacheKey.Latest(context.UserId, context.ChatboxId);

        Guid draftId;
        OrderDraft? draft;
        if (requested is { } rid && _cache.TryGetValue(OrderDraftCacheKey.For(context.UserId, rid), out draft) && draft is not null)
        {
            draftId = rid;
        }
        else if (_cache.TryGetValue(latestKey, out Guid latestId)
            && _cache.TryGetValue(OrderDraftCacheKey.For(context.UserId, latestId), out draft) && draft is not null)
        {
            draftId = latestId;
        }
        else
        {
            return ToolArgs.Error("No active draft found (expired or already used) — call prepare_order again.");
        }

        // 1 lần dùng: mọi lượt gọi sau (kể cả khi lỗi bên dưới) đều báo hết hạn.
        _cache.Remove(OrderDraftCacheKey.For(context.UserId, draftId));
        _cache.Remove(latestKey);

        var paymentMethod = PaymentMethod.PayOS;
        var paymentMethodText = ToolArgs.GetString(arguments, "paymentMethod");
        if (!string.IsNullOrWhiteSpace(paymentMethodText) && !Enum.TryParse(paymentMethodText, true, out paymentMethod))
            return ToolArgs.Error("Invalid 'paymentMethod' — must be 'PayOS' or 'COD'.");

        var checkoutRequest = new CheckoutRequest
        {
            ShippingAddressId = draft.ShippingAddressId,
            Items = new List<CheckoutItemRequest> { new() { ProductItemId = draft.ProductItemId, Quantity = draft.Quantity } },
            PaymentMethod = paymentMethod,
        };

        // Re-validate giá/tồn kho trước khi tạo đơn thật — không tin snapshot cũ trong draft.
        var previewResult = await _orders.PreviewShippingFeeAsync(context.UserId, checkoutRequest, ct);
        if (!previewResult.IsSuccess || previewResult.Data is null)
            return ToolArgs.Error(previewResult.Message ?? "Could not re-validate the order — call prepare_order again.");

        var expectedSubtotal = draft.UnitPriceSnapshot * draft.Quantity;
        if (previewResult.Data.Subtotal != expectedSubtotal)
        {
            var newUnitPrice = previewResult.Data.Subtotal / draft.Quantity;
            return ToolArgs.Error(
                $"The price changed since prepare_order (was {draft.UnitPriceSnapshot:#,0}đ, now {newUnitPrice:#,0}đ). " +
                "No order was created — tell the user the new price and call prepare_order again if they still want to proceed.");
        }

        var checkoutResult = await _orders.CheckoutAsync(context.UserId, checkoutRequest, ct);
        if (!checkoutResult.IsSuccess || checkoutResult.Data is null)
            return ToolArgs.Error(checkoutResult.Message ?? "Could not place the order.");
        var order = checkoutResult.Data;

        if (paymentMethod != PaymentMethod.PayOS)
        {
            return ToolArgs.Json(new
            {
                orderId = order.Id,
                status = order.Status.ToString(),
                checkoutUrl = (string?)null,
                expiresInMinutes = (int?)null,
            });
        }

        var paymentResult = await _payments.CreatePaymentAsync(order.Id, context.UserId, ct);
        if (!paymentResult.IsSuccess || paymentResult.Data is null)
        {
            return ToolArgs.Json(new
            {
                orderId = order.Id,
                status = order.Status.ToString(),
                checkoutUrl = (string?)null,
                expiresInMinutes = 15,
                warning = paymentResult.Message ?? "Order created, but the payment link could not be generated — tell the user to retry from their order page.",
            });
        }

        return ToolArgs.Json(new
        {
            orderId = order.Id,
            status = order.Status.ToString(),
            checkoutUrl = paymentResult.Data.CheckoutUrl,
            expiresInMinutes = 15,
        });
    }
}
