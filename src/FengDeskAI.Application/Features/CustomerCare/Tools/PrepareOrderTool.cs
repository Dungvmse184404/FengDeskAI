using System.Text.Json;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Enums.Payment;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Tạo MỚI hoặc SỬA draft đơn hàng (một sản phẩm) của phòng chat — lưu bảng <c>ai_order_drafts</c>,
/// mỗi phòng tối đa 1 draft đang mở. Khi đã có draft, tham số nào không truyền sẽ giữ nguyên giá trị
/// trong draft (đổi số lượng chỉ cần gửi <c>quantity</c>). Thiếu variant/địa chỉ thì trả "missing" để
/// AI hỏi lại user thay vì tự đoán — draft cũ (nếu có) giữ nguyên.
/// Chỉ enable ở phòng riêng user↔AI (xem <see cref="AiToolContext.IsPrivateRoom"/>).
/// </summary>
public sealed class PrepareOrderTool : IAiTool
{
    private const int MaxQuantity = 10;

    private readonly IProductService _products;
    private readonly IOrderService _orders;
    private readonly IUnitOfWork _uow;
    private readonly AiOrderDraftOptions _options;

    public PrepareOrderTool(IProductService products, IOrderService orders, IUnitOfWork uow, IOptions<AiOrderDraftOptions> options)
    {
        _products = products;
        _orders = orders;
        _uow = uow;
        _options = options.Value;
    }

    public string Name => "prepare_order";

    public string Description =>
        "Create the user's draft order for ONE product: resolves the variant, checks stock and the shipping " +
        "address (user's default, or a saved one via shippingAddressId) and previews the shipping fee. " +
        "Returns a summary - show it to the user and ask them to confirm. The order is NOT placed yet.";

    public string DescribeFor(AiToolContext context) => context.ActiveOrderDraft is null
        ? Description
        : "EDIT the user's current draft order (shown in CURRENT ORDER DRAFT) when they want to change the " +
          "product, variant, quantity, shipping address or payment method. Pass ONLY the fields that change - " +
          "everything else is kept from the draft. Returns the updated summary - show it and ask the user to " +
          "confirm again. Do NOT call this when the user simply agrees to the current draft.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["productId"] = new("string", "Product id (GUID) from a search/recommend result. Required when there is no " +
            "draft yet; when editing, pass it only to switch to a different product."),
        ["quantity"] = new("integer", $"Quantity to buy (1..{MaxQuantity}). Omit to keep the current one (new draft: 1)."),
        ["productItemId"] = new("string", "Specific variant id (GUID) - needed when the product has several " +
            "variants (ask the user to pick one first)."),
        ["shippingAddressId"] = new("string", "Id of a saved address (GUID) from list_my_addresses, when the user " +
            "wants to ship somewhere other than the current/default address."),
        ["paymentMethod"] = new("string", "Payment method. Default PayOS; use COD only when the user asks for it.",
            Enum: new[] { "PayOS", "COD" }),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        if (context.ChatboxId is not { } chatboxId)
            return ToolArgs.Error("Could not determine the chat room.");

        var now = DateTime.UtcNow;
        var existing = await _uow.AiOrderDrafts.GetPendingForUpdateAsync(context.UserId, chatboxId, now, ct);

        // Tham số có gửi nhưng sai định dạng → báo lỗi, KHÔNG âm thầm rơi về giá trị cũ trong draft.
        if (HasInvalidGuid(arguments, "productId") || HasInvalidGuid(arguments, "productItemId")
            || HasInvalidGuid(arguments, "shippingAddressId"))
            return ToolArgs.Error("An id parameter is not a valid GUID - use the exact id from a tool result.");

        var paymentMethodText = ToolArgs.GetString(arguments, "paymentMethod");
        var requestedPaymentMethod = ToolArgs.GetEnum<PaymentMethod>(arguments, "paymentMethod");
        if (!string.IsNullOrWhiteSpace(paymentMethodText) && requestedPaymentMethod is null)
            return ToolArgs.Error("Invalid 'paymentMethod' - must be 'PayOS' or 'COD'.");

        var productId = ToolArgs.GetGuid(arguments, "productId") ?? existing?.ProductId;
        if (productId is null)
            return ToolArgs.Error("Missing 'productId' - there is no draft yet, so pass the product id from a search/recommend result.");

        // Đổi sang sản phẩm khác → variant/số lượng cũ không còn nghĩa; cùng sản phẩm → giữ từ draft.
        var keepsProduct = existing is not null && existing.ProductId == productId;
        var quantity = Math.Clamp(ToolArgs.GetInt(arguments, "quantity") ?? (keepsProduct ? existing!.Quantity : 1), 1, MaxQuantity);
        var productItemId = ToolArgs.GetGuid(arguments, "productItemId") ?? (keepsProduct ? existing!.ProductItemId : null);
        var shippingAddressId = ToolArgs.GetGuid(arguments, "shippingAddressId") ?? existing?.ShippingAddressId;
        var paymentMethod = requestedPaymentMethod ?? existing?.PaymentMethod ?? PaymentMethod.PayOS;

        var productResult = await _products.GetByIdAsync(productId.Value, ct);
        if (!productResult.IsSuccess || productResult.Data is null)
            return ToolArgs.Error(productResult.Message ?? "Product not found.");
        var product = productResult.Data;
        if (!product.IsActive)
            return ToolArgs.Error("This product is no longer for sale.");

        // 1) Resolve variant: explicit id, or auto-pick when there's only one.
        var item = productItemId is { } piid
            ? product.Items.FirstOrDefault(i => i.Id == piid)
            : product.Items.Count == 1 ? product.Items[0] : null;

        if (item is null)
        {
            if (product.Items.Count == 0)
                return ToolArgs.Error("This product has no purchasable variant.");
            if (productItemId is not null)
                return ToolArgs.Error("Variant not found for this product.");

            return ToolArgs.Json(new
            {
                summary = (object?)null,
                missing = new[] { "variant" },
                variants = product.Items.Select(i => new { i.Id, i.Name, i.Price, i.Stock }),
                fixLinks = (object?)null,
                note = "Ask the user which variant they want, then call prepare_order again with productItemId set." +
                       (existing is null ? string.Empty : " The current draft is unchanged until then."),
            });
        }

        if (quantity > item.Stock)
            return ToolArgs.Error($"Only {item.Stock} unit(s) of this variant left in stock - ask the user to lower the quantity.");

        // 2) Địa chỉ: id user chọn / đã lưu trong draft (phải thuộc user) hoặc địa chỉ mặc định.
        // Tool không bao giờ cho AI tự bịa địa chỉ — chỉ id user thật sự sở hữu.
        UserAddress? chosenAddress;
        if (shippingAddressId is { } saId)
        {
            chosenAddress = await _uow.UserAddresses.GetByIdForUserAsync(saId, context.UserId, ct);
            if (chosenAddress is null)
                return ToolArgs.Error("Address not found - call list_my_addresses to see valid saved addresses.");
        }
        else
        {
            chosenAddress = await _uow.UserAddresses.GetDefaultForUserAsync(context.UserId, ct);
            if (chosenAddress is null)
            {
                return ToolArgs.Json(new
                {
                    summary = (object?)null,
                    missing = new[] { "address" },
                    fixLinks = new { address = "/profile/addresses" },
                    note = "The user has no default shipping address. Tell them to add one at the link, then call prepare_order again once they say they're done.",
                });
            }
        }
        var address = await _uow.UserAddresses.GetWithWardChainAsync(chosenAddress.Id, ct) ?? chosenAddress;

        // 3) Preview phí ship (OrderService cũng kiểm lại tồn kho/trạng thái bán).
        var checkoutRequest = new CheckoutRequest
        {
            ShippingAddressId = address.Id,
            Items = new List<CheckoutItemRequest> { new() { ProductItemId = item.Id, Quantity = quantity } },
            PaymentMethod = paymentMethod,
        };
        var previewResult = await _orders.PreviewShippingFeeAsync(context.UserId, checkoutRequest, ct);
        if (!previewResult.IsSuccess || previewResult.Data is null)
            return ToolArgs.Error(previewResult.Message ?? "Could not preview the order.");
        var preview = previewResult.Data;

        // 4) Lưu draft: sửa tại chỗ nếu đã có, không thì tạo mới.
        var draft = existing;
        if (draft is null)
        {
            // Dọn draft Pending đã hết hạn còn sót (worker chưa kịp xóa) — nếu không unique index chặn insert.
            await _uow.AiOrderDrafts.DeletePendingAsync(context.UserId, chatboxId, ct);
            draft = new AiOrderDraft { UserId = context.UserId, ChatboxId = chatboxId };
            await _uow.AiOrderDrafts.AddAsync(draft, ct);
        }

        draft.ProductId = product.Id;
        draft.ProductItemId = item.Id;
        draft.Quantity = quantity;
        draft.ShippingAddressId = address.Id;
        draft.PaymentMethod = paymentMethod;
        draft.UnitPriceSnapshot = item.Price;
        // Phí ship user thực trả (đã trừ giảm phí từ voucher) → subtotal + shippingFee = total.
        draft.ShippingFeeSnapshot = preview.TotalShippingFee - preview.ShippingDiscount;
        draft.TotalAmountSnapshot = preview.TotalAmount;
        draft.ProductName = product.Name;
        draft.VariantName = item.Name;
        draft.AddressText = FormatAddress(address);
        draft.ExpiresAt = now.AddMinutes(_options.DraftTtlMinutes);
        await _uow.SaveChangesAsync(ct);

        context.OrderDraftChangedThisTurn = true;
        context.Products.Add(new AiProductRef(product.Id, product.Name));

        return ToolArgs.Json(new
        {
            action = existing is null ? "created" : "updated",
            summary = draft.ToToolSummary(),
            missing = Array.Empty<string>(),
            fixLinks = (object?)null,
            note = "Show this summary to the user in a table and ask them to confirm. The order is NOT placed yet - " +
                   "do not call any other ordering tool in this turn.",
        });
    }

    private static bool HasInvalidGuid(JsonElement arguments, string name)
        => !string.IsNullOrWhiteSpace(ToolArgs.GetString(arguments, name)) && ToolArgs.GetGuid(arguments, name) is null;

    private static string FormatAddress(UserAddress address)
        => $"{address.RecipientName} ({address.RecipientPhone}) - {address.StreetAddress}, " +
           $"{address.Ward.Name}, {address.Ward.District.Name}, {address.Ward.District.Province.Name}";
}
