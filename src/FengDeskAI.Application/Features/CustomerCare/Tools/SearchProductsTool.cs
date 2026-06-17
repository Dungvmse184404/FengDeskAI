using System.Text.Json;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Tìm sản phẩm theo từ khoá (đọc, public).</summary>
public sealed class SearchProductsTool : IAiTool
{
    private const int MaxLimit = 8;
    private readonly IProductService _products;

    public SearchProductsTool(IProductService products) => _products = products;

    public string Name => "search_products";
    public string Description => "Tìm sản phẩm phong thủy theo từ khoá (tên). Trả về danh sách id, tên, giá, ảnh.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["query"] = new("string", "Từ khoá tìm kiếm (tên sản phẩm).", Required: true),
        ["limit"] = new("integer", $"Số kết quả tối đa (mặc định {MaxLimit})."),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var query = ToolArgs.GetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return ToolArgs.Error("Thiếu tham số 'query'.");

        var limit = Math.Clamp(ToolArgs.GetInt(arguments, "limit") ?? MaxLimit, 1, MaxLimit);
        var result = await _products.SearchAsync(new ProductQueryParams { Search = query, Page = 1, PageSize = limit }, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Tìm kiếm thất bại.");

        return ToolArgs.Json(new { total = result.Data.TotalCount, items = result.Data.Items });
    }
}
