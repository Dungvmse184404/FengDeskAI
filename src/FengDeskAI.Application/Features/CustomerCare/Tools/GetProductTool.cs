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
    public string Description => "Lấy chi tiết một sản phẩm theo id: tên, mô tả, biến thể/giá, ảnh, danh mục.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["productId"] = new("string", "Id (GUID) của sản phẩm.", Required: true),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var id = ToolArgs.GetGuid(arguments, "productId");
        if (id is null)
            return ToolArgs.Error("Thiếu hoặc sai 'productId' (phải là GUID).");

        var result = await _products.GetByIdAsync(id.Value, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Không tìm thấy sản phẩm.");

        return ToolArgs.Json(result.Data);
    }
}
