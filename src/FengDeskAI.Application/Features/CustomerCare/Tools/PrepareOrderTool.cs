using System.Text.Json;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Geography;
using Microsoft.Extensions.Caching.Memory;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Chuẩn bị draft đơn hàng cho MỘT sản phẩm — chỉ ghi vào cache tạm (TTL 15'), không đụng DB.
/// Thiếu variant hay địa chỉ giao thì trả về "missing" để AI hỏi lại user thay vì tự đoán.
/// Chỉ enable ở phòng riêng user↔AI (xem <see cref="AiToolContext.IsPrivateRoom"/>).
/// </summary>
public sealed class PrepareOrderTool : IAiTool
{
    private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(15);
    private const int MaxQuantity = 10;

    private readonly IProductService _products;
    private readonly IOrderService _orders;
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;

    public PrepareOrderTool(IProductService products, IOrderService orders, IUnitOfWork uow, IMemoryCache cache)
    {
        _products = products;
        _orders = orders;
        _uow = uow;
        _cache = cache;
    }

    public string Name => "prepare_order";

    public string Description =>
        "Prepare a draft order for ONE product before checkout: resolves the variant, checks stock and the " +
        "shipping address (user's default, or a specific saved one via shippingAddressId), and previews the " +
        "shipping fee. Returns a draftId + summary — read the summary back to the user and WAIT for their " +
        "explicit confirmation before calling confirm_order. Never invent a draftId; it must come from this tool's result.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["productId"] = new("string", "Product id (GUID) — from a prior recommend/search result.", Required: true),
        ["quantity"] = new("integer", $"Quantity to buy (default 1, max {MaxQuantity})."),
        ["productItemId"] = new("string", "Specific variant id (GUID) — required only when the product has multiple variants (ask the user to pick one first)."),
        ["shippingAddressId"] = new("string", "Id of a saved address (GUID) — from list_my_addresses, when the user wants to ship to a " +
            "specific address instead of their default. Omit to use their default address."),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var productId = ToolArgs.GetGuid(arguments, "productId");
        if (productId is null)
            return ToolArgs.Error("Missing or invalid 'productId' (must be a GUID).");

        var quantity = Math.Clamp(ToolArgs.GetInt(arguments, "quantity") ?? 1, 1, MaxQuantity);
        var productItemId = ToolArgs.GetGuid(arguments, "productItemId");

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
                draftId = (Guid?)null,
                summary = (object?)null,
                missing = new[] { "variant" },
                variants = product.Items.Select(i => new { i.Id, i.Name, i.Price, i.Stock }),
                fixLinks = (object?)null,
                note = "Ask the user which variant they want, then call prepare_order again with productItemId set.",
            });
        }

        if (quantity > item.Stock)
            return ToolArgs.Error($"Only {item.Stock} unit(s) of this variant left in stock — ask the user to lower the quantity.");

        // 2) Resolve the shipping address: a specific saved one (via shippingAddressId, must belong to the
        // user) or the default. This tool never lets the AI invent an address — only ids the user actually owns.
        var shippingAddressId = ToolArgs.GetGuid(arguments, "shippingAddressId");
        UserAddress? chosenAddress;
        if (shippingAddressId is { } saId)
        {
            chosenAddress = await _uow.UserAddresses.GetByIdForUserAsync(saId, context.UserId, ct);
            if (chosenAddress is null)
                return ToolArgs.Error("Address not found — call list_my_addresses to see valid saved addresses.");
        }
        else
        {
            chosenAddress = await _uow.UserAddresses.GetDefaultForUserAsync(context.UserId, ct);
            if (chosenAddress is null)
            {
                return ToolArgs.Json(new
                {
                    draftId = (Guid?)null,
                    summary = (object?)null,
                    missing = new[] { "address" },
                    fixLinks = new { address = "/profile/addresses" },
                    note = "The user has no default shipping address. Tell them to add one at the link, then call prepare_order again once they say they're done.",
                });
            }
        }
        var address = await _uow.UserAddresses.GetWithWardChainAsync(chosenAddress.Id, ct) ?? chosenAddress;

        // 3) Preview shipping fee (also re-checks stock/active status inside OrderService).
        var checkoutRequest = new CheckoutRequest
        {
            ShippingAddressId = address.Id,
            Items = new List<CheckoutItemRequest> { new() { ProductItemId = item.Id, Quantity = quantity } },
        };
        var previewResult = await _orders.PreviewShippingFeeAsync(context.UserId, checkoutRequest, ct);
        if (!previewResult.IsSuccess || previewResult.Data is null)
            return ToolArgs.Error(previewResult.Message ?? "Could not preview the order.");
        var preview = previewResult.Data;

        // 4) Save draft (cache only — no DB write).
        var draftId = Guid.NewGuid();
        var draft = new OrderDraft(context.UserId, item.Id, quantity, item.Price, address.Id, DateTime.UtcNow);
        _cache.Set(OrderDraftCacheKey.For(context.UserId, draftId), draft, DraftTtl);
        // Pointer draft mới nhất theo user+phòng — confirm_order fallback khi model không còn nhớ draftId.
        _cache.Set(OrderDraftCacheKey.Latest(context.UserId, context.ChatboxId), draftId, DraftTtl);

        context.Products.Add(new AiProductRef(product.Id, product.Name));

        var variantName = item.Name is null ? product.Name : $"{product.Name} - {item.Name}";
        var addressText = $"{address.RecipientName} ({address.RecipientPhone}) - {address.StreetAddress}, " +
                           $"{address.Ward.Name}, {address.Ward.District.Name}, {address.Ward.District.Province.Name}";

        return ToolArgs.Json(new
        {
            draftId,
            summary = new
            {
                productName = product.Name,
                variant = variantName,
                quantity,
                unitPrice = item.Price,
                shippingFee = preview.TotalShippingFee,
                total = preview.TotalAmount,
                addressText,
            },
            missing = Array.Empty<string>(),
            fixLinks = (object?)null,
            note = "Read the summary back to the user and WAIT for their explicit confirmation before calling confirm_order. " +
                   "Never call confirm_order without a clear go-ahead in the user's next message.",
        });
    }
}
