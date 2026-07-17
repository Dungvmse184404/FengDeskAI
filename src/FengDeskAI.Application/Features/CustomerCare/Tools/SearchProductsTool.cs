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
    public string Description =>
        "Search feng shui products by keyword — scans NAME, DESCRIPTION, CATEGORY names and feng shui " +
        "ELEMENTS (e.g. 'hỏa' finds Fire-element products), diacritics- and case-insensitive. Multi-word " +
        "queries match all words first, then automatically relax to any-word if nothing matches — so pass " +
        "the user's phrase as-is; no need to retry with shorter keywords. Returns id, name, image and " +
        "variants with price + stock (items).";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["query"] = new("string", "Search keyword (name or description; e.g. 'Hỏa', 'để bàn', 'kim loại').", Required: true),
        ["element"] = new("string", "Optional STRICT feng shui element filter (matches the product's primary or secondary element). " +
            "Use codes from compute_destiny_chart's favorableElementCodes.", Enum: new[] { "Kim", "Moc", "Thuy", "Hoa", "Tho" }),
        ["limit"] = new("integer", $"Maximum number of results (default {MaxLimit})."),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var query = ToolArgs.GetString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query))
            return ToolArgs.Error("Missing 'query' parameter.");

        // Filter hành tường minh (deterministic) — bổ trợ cho keyword search vốn chỉ match tên hành trong text.
        Domain.Enums.Workspace.FengShuiElement? element = null;
        if (Enum.TryParse<Domain.Enums.Workspace.FengShuiElement>(ToolArgs.GetString(arguments, "element"), true, out var parsed))
            element = parsed;

        var limit = Math.Clamp(ToolArgs.GetInt(arguments, "limit") ?? MaxLimit, 1, MaxLimit);
        var result = await _products.SearchAsync(new ProductQueryParams { Search = query, Element = element, Page = 1, PageSize = limit }, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Search failed.");

        return ToolArgs.Json(new { total = result.Data.TotalCount, items = result.Data.Items });
    }
}
