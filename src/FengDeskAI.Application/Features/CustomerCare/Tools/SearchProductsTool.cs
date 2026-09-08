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
        ["aspiration"] = new("string", "Optional filter by the feng shui GOAL the user states (\"I want something for wealth\"). " +
            "Only products an admin approved for that goal are returned. Omit when they don't mention a goal; " +
            "if their goal seems relevant but unclear, ask them which one.",
            Enum: new[] { "Wealth", "Career", "Health", "Relationship", "Study" }),
        ["placement"] = new("string", "How the item is used. DEFAULTS TO 'Desk' — omit it for ordinary desk/room " +
            "decor. Pass 'Carry' only when the user asks for something to WEAR or CARRY (bracelet, pendant, " +
            "keyring, wallet charm); 'Living' for plants and living things; 'Consumable' for incense, candles, " +
            "salt and other things that get used up.",
            Enum: new[] { "Desk", "Living", "Carry", "Consumable" }),
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

        // Mục tiêu phong thủy user nêu — lọc cứng, chỉ thẻ đã được admin duyệt.
        Domain.Enums.Catalog.Aspiration? aspiration = null;
        if (Enum.TryParse<Domain.Enums.Catalog.Aspiration>(ToolArgs.GetString(arguments, "aspiration"), true, out var parsedAspiration))
            aspiration = parsedAspiration;

        // Q10 — mặc định Desk khi model không truyền. Không mặc định thì "vòng tay Kim" và "đèn muối"
        // rơi vào cùng một rổ, mà hai thứ đó đi HAI luồng chấm điểm khác nhau (gap phòng vs dụng thần).
        // Desk là nhóm đông nhất nên mặc định đó ít gây bất ngờ nhất cho câu hỏi chung chung.
        var placement = Enum.TryParse<Domain.Enums.Catalog.ProductPlacement>(
            ToolArgs.GetString(arguments, "placement"), true, out var parsedPlacement)
            ? parsedPlacement
            : Domain.Enums.Catalog.ProductPlacement.Desk;

        var limit = Math.Clamp(ToolArgs.GetInt(arguments, "limit") ?? MaxLimit, 1, MaxLimit);
        var result = await _products.SearchAsync(
            new ProductQueryParams
            {
                Search = query,
                Element = element,
                Aspiration = aspiration,
                Placement = placement,
                Page = 1,
                PageSize = limit,
            }, ct);
        if (result.IsSuccess && result.Data is { TotalCount: 0 } && aspiration is not null)
            return ToolArgs.Json(new
            {
                total = 0,
                items = Array.Empty<object>(),
                note = $"No product is approved for the goal \"{aspiration}\" yet. Tell the user, then offer to search "
                    + "without that filter instead of silently dropping it.",
            });
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Search failed.");

        return ToolArgs.Json(new { total = result.Data.TotalCount, items = result.Data.Items });
    }
}
