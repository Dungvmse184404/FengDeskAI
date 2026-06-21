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
    public string Description => "Get a product's details by id: name, description, variants/price, images, category.";

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

        return ToolArgs.Json(result.Data);
    }
}
