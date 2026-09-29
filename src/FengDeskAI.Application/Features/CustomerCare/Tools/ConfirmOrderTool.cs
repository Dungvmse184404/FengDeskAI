using System.Text.Json;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Enums.Payment;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Đặt đơn thật từ draft đang mở của phòng (bảng <c>ai_order_drafts</c>). KHÔNG nhận id nào — server tự lấy
/// draft của (user, phòng) nên model không thể chỉ định nhầm hay bịa đơn.
/// Chốt chặn: (1) chỉ đưa cho LLM khi đầu lượt đã có draft; (2) từ chối nếu draft vừa tạo/sửa trong lượt này
/// (user chưa thấy bản mới); (3) chiếm draft bằng UPDATE có điều kiện — 2 lượt confirm song song chỉ 1 lượt
/// tạo được đơn. Giá đổi / checkout lỗi → draft trả về Pending, user không mất những gì đã chọn.
/// Chỉ enable ở phòng riêng user↔AI (xem <see cref="AiToolContext.IsPrivateRoom"/>).
/// </summary>
public sealed class ConfirmOrderTool : IAiTool
{
    private readonly IOrderService _orders;
    private readonly IPaymentService _payments;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ConfirmOrderTool> _logger;

    public ConfirmOrderTool(IOrderService orders, IPaymentService payments, IUnitOfWork uow, ILogger<ConfirmOrderTool> logger)
    {
        _orders = orders;
        _payments = payments;
        _uow = uow;
        _logger = logger;
    }

    public string Name => "confirm_order";

    public string Description =>
        "Place the real order from the user's current draft order. Takes no id - the system knows which draft is theirs.";

    public string DescribeFor(AiToolContext context) =>
        "Place the real order from the user's CURRENT ORDER DRAFT (shown to you in that block). Call this RIGHT AWAY " +
        "when the user's latest message agrees to the draft (e.g. \"ok\", \"chốt\", \"đặt đi\", \"yes\") - do NOT call " +
        "prepare_order again and do NOT ask for confirmation a second time. Takes no id. Pass paymentMethod only if " +
        "the user just asked to change it in this message.";

    /// <summary>Chưa có draft lúc đầu lượt → không đưa tool cho LLM (không có gì để xác nhận).</summary>
    public bool IsAvailable(AiToolContext context) => context.ActiveOrderDraft is not null;

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["paymentMethod"] = new("string", "Only if the user asked to change the payment method in their latest message. " +
            "Omit to use the method saved in the draft.", Enum: new[] { "PayOS", "COD" }),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        if (context.ChatboxId is not { } chatboxId)
            return ToolArgs.Error("Could not determine the chat room.");

        if (context.OrderDraftChangedThisTurn)
            return ToolArgs.Error("The draft was just created or changed in this turn, so the user has not seen this " +
                "version yet. No order was created - show the summary and wait for their confirmation in their NEXT message.");

        var paymentMethodText = ToolArgs.GetString(arguments, "paymentMethod");
        var requestedPaymentMethod = ToolArgs.GetEnum<PaymentMethod>(arguments, "paymentMethod");
        if (!string.IsNullOrWhiteSpace(paymentMethodText) && requestedPaymentMethod is null)
            return ToolArgs.Error("Invalid 'paymentMethod' - must be 'PayOS' or 'COD'.");

        var now = DateTime.UtcNow;
        var draft = await _uow.AiOrderDrafts.GetPendingAsync(context.UserId, chatboxId, now, ct);
        if (draft is null)
            return ToolArgs.Error("There is no active draft order (expired, discarded or already placed) - call prepare_order to create one.");

        if (!await _uow.AiOrderDrafts.TryClaimAsync(draft.Id, now, ct))
            return ToolArgs.Error("This draft is already being placed by another request - do not retry; check list_my_orders instead.");

        var paymentMethod = requestedPaymentMethod ?? draft.PaymentMethod;
        var checkoutRequest = new CheckoutRequest
        {
            ShippingAddressId = draft.ShippingAddressId,
            Items = new List<CheckoutItemRequest> { new() { ProductItemId = draft.ProductItemId, Quantity = draft.Quantity } },
            PaymentMethod = paymentMethod,
        };

        OrderDetailResponse order;
        try
        {
            // Re-validate giá/tồn kho trước khi tạo đơn thật — không tin snapshot cũ trong draft.
            var previewResult = await _orders.PreviewShippingFeeAsync(context.UserId, checkoutRequest, ct);
            if (!previewResult.IsSuccess || previewResult.Data is null)
            {
                await _uow.AiOrderDrafts.ReleaseClaimAsync(draft.Id, DateTime.UtcNow, ct);
                return ToolArgs.Error((previewResult.Message ?? "Could not re-validate the order.") +
                    " No order was created; the draft is kept - tell the user and let them adjust it.");
            }

            var preview = previewResult.Data;
            var expectedSubtotal = draft.UnitPriceSnapshot * draft.Quantity;
            if (preview.Subtotal != expectedSubtotal)
            {
                var newUnitPrice = preview.Subtotal / draft.Quantity;
                await _uow.AiOrderDrafts.ReleaseClaimWithNewPriceAsync(draft.Id, newUnitPrice,
                    preview.TotalShippingFee - preview.ShippingDiscount, preview.TotalAmount, DateTime.UtcNow, ct);
                // Draft vừa đổi giá → user phải thấy giá mới trước; chặn confirm lại ngay trong lượt này.
                context.OrderDraftChangedThisTurn = true;
                return ToolArgs.Error(
                    $"The price changed (was {draft.UnitPriceSnapshot:#,0}đ, now {newUnitPrice:#,0}đ; new total " +
                    $"{preview.TotalAmount:#,0}đ). No order was created - the draft now has the new price. Tell the user " +
                    "and ask them to confirm again.");
            }

            var checkoutResult = await _orders.CheckoutAsync(context.UserId, checkoutRequest, ct);
            if (!checkoutResult.IsSuccess || checkoutResult.Data is null)
            {
                await _uow.AiOrderDrafts.ReleaseClaimAsync(draft.Id, DateTime.UtcNow, ct);
                return ToolArgs.Error((checkoutResult.Message ?? "Could not place the order.") +
                    " No order was created; the draft is kept - tell the user and let them adjust it.");
            }
            order = checkoutResult.Data;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Lỗi bất ngờ trước khi có đơn → trả draft về Pending, đừng để kẹt Confirming rồi bị worker xóa mất.
            await _uow.AiOrderDrafts.ReleaseClaimAsync(draft.Id, DateTime.UtcNow, CancellationToken.None);
            throw;
        }

        // Đơn đã tạo → xóa draft NGAY (trước khi gọi PayOS) để không thể đặt trùng.
        await _uow.AiOrderDrafts.DeleteAsync(draft.Id, CancellationToken.None);
        _logger.LogInformation(
            "[AiOrder] Draft {DraftId} → order {OrderId} (user {UserId}, item {ProductItemId} x{Quantity}, {PaymentMethod}).",
            draft.Id, order.Id, context.UserId, draft.ProductItemId, draft.Quantity, paymentMethod);

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
                warning = paymentResult.Message ?? "Order created, but the payment link could not be generated - tell the user to retry from their order page.",
            });
        }

        // Ghi vào context để AiChatService gắn card thanh toán (QR + nút) vào cuối tin nhắn — FE render,
        // model KHÔNG cần (và không nên) tự chép link.
        context.Payment = new AiPaymentRef(
            order.Id, paymentResult.Data.Amount, paymentResult.Data.CheckoutUrl,
            paymentResult.Data.QrCode, ExpiresInMinutes: 15);

        return ToolArgs.Json(new
        {
            orderId = order.Id,
            status = order.Status.ToString(),
            expiresInMinutes = 15,
            note = "Payment link and QR code are ALREADY displayed to the user as an attachment below your reply. " +
                   "Do NOT repeat or invent any payment URL - just confirm the order and remind them to pay within 15 minutes.",
        });
    }
}
