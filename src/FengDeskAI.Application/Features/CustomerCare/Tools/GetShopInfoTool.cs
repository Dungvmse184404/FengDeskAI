using System.Text.Json;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Trong phòng chat khách ↔ 1 shop cụ thể (Chatbox.GardenStoreId != null — khách bấm "Chat ngay" trên trang
/// shop): tra thông tin shop (ngày tham gia, đánh giá trung bình gộp từ review sản phẩm, vài sản phẩm nổi bật)
/// để AI giới thiệu/tư vấn thay nhân viên khi chưa có ai vào hỗ trợ. Không tham số — tự suy shop từ phòng
/// đang chat. Vô tác dụng (trả lỗi) nếu phòng không gắn shop nào (vd phòng hỗ trợ nền tảng chung).
/// </summary>
public sealed class GetShopInfoTool : IAiTool
{
    private const int ProductLimit = 5;

    private readonly IUnitOfWork _uow;
    private readonly IStoreService _stores;
    private readonly IProductService _products;

    public GetShopInfoTool(IUnitOfWork uow, IStoreService stores, IProductService products)
    {
        _uow = uow;
        _stores = stores;
        _products = products;
    }

    public string Name => "get_shop_info";
    public string Description =>
        "Get info about the shop this conversation room belongs to: join date, average rating (aggregated " +
        "from product reviews), and a few featured products. Only works in a conversation room linked to a " +
        "specific shop (customer chatting with a store via its 'Chat ngay' button). Returns an error if this " +
        "room is not linked to any shop (e.g. the general platform support room).";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        if (context.ChatboxId is not { } chatboxId)
            return ToolArgs.Error("Could not determine the chat room.");

        var room = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (room?.GardenStoreId is not { } storeId)
            return ToolArgs.Error("This room is not linked to any shop.");

        var storeResult = await _stores.GetByIdAsync(storeId, ct);
        if (!storeResult.IsSuccess || storeResult.Data is null)
            return ToolArgs.Error("Shop not found.");
        var store = storeResult.Data;

        var (avgRating, reviewCount) = await _uow.Reviews.GetStoreRatingSummaryAsync(storeId, ct);

        var productsResult = await _products.SearchAsync(
            new ProductQueryParams { StoreId = storeId, Page = 1, PageSize = ProductLimit }, ct);
        var products = productsResult.IsSuccess && productsResult.Data is not null
            ? productsResult.Data.Items.Select(p => new
            {
                p.Id,
                p.Name,
                p.MinPrice,
                p.PrimaryImageUrl,
            })
            : Enumerable.Empty<object>();

        return ToolArgs.Json(new
        {
            name = store.Name,
            description = store.Description,
            hotline = store.Hotline,
            joinedAt = store.CreatedAt,
            rating = new { average = Math.Round(avgRating, 1), reviewCount },
            products,
        });
    }
}
