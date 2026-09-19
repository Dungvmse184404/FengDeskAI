using System.Text.Json;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Lấy chi tiết 1 sản phẩm theo id (đọc, public).</summary>
public sealed class GetProductTool : IAiTool
{
    private readonly IProductService _products;

    public GetProductTool(IProductService products) => _products = products;

    public string Name => "get_product";

    public string Description =>
        "Get one product's details by id: name, description, variants (price/stock/size), images, " +
        "categories, and its feng shui attributes (elements, vibes, placement, approved goals). " +
        "Use after search_products / recommend_products when the user asks about a specific item.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["productId"] = new("string", "Product id (GUID).", Required: true),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var id = ToolArgs.GetGuid(arguments, "productId");
        if (id is null)
            return ToolArgs.Error("Missing or invalid 'productId' (must be a GUID).");

        var result = await _products.GetByIdAsync(id.Value, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Product not found.");

        var p = result.Data;

        // Đăng ký vào registry của lượt để AiChatService.LinkifyProducts tự chèn link nếu model quên.
        // Thiếu bước này thì model chỉ có GUID trần → AiTextSanitizer(UserMessage) cắt còn "xxxxxxxx-..."
        // và user không bấm được vào đâu cả.
        context.Products.Add(new AiProductRef(p.Id, p.Name));

        // Chiếu lại payload thay vì ToolArgs.Json(result.Data): DTO đầy đủ mang theo gardenStoreId,
        // createdAt/updatedAt, id của từng ảnh/model3D — toàn GUID nhiễu, model dễ chép nhầm vào câu
        // trả lời rồi bị censor. Chỉ đưa thứ model thật sự cần để tư vấn.
        return ToolArgs.Json(new
        {
            p.Id,
            p.Name,
            Link = $"[{p.Name}](/products/{p.Id})",
            p.Description,
            p.StoreName,
            Categories = p.Categories.Select(c => c.Name).ToList(),

            // ── Phong thủy ──
            p.PrimaryElement,
            p.SecondaryElements,
            p.Vibes,
            // Desk | Living | Carry | Consumable — quyết định vật dùng ở đâu.
            p.Placement,
            // Mục tiêu ĐÃ được sàn duyệt (Wealth/Career/Health/Relationship/Study). Rỗng = chưa duyệt
            // thẻ nào; ĐỪNG tự suy ra sản phẩm hợp mục tiêu gì khi danh sách này rỗng.
            ApprovedGoals = p.Aspirations,

            // ── Mua hàng ──
            Items = p.Items.Select(i => new { i.Id, i.Name, i.Price, i.Stock, i.SizeClass }).ToList(),
            ImageUrl = p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
            Has3DModel = p.Models3D.Count > 0,

            Note = "When mentioning this product, write its name EXACTLY as the 'link' value (a markdown "
                + "link). Never paste a raw id into your reply. 'placement' says where the item is used - "
                + "do not give compass placement advice for Carry items.",
        });
    }
}
