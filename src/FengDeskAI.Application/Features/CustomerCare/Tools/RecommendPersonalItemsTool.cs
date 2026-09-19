using System.Text.Json;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// Gợi ý vật phẩm MANG THEO NGƯỜI cho CHÍNH user (scope theo userId) — chấm theo dụng thần/bản mệnh,
/// không cần workspace. Xem <c>docs/adr/product-placement-personal-recommendation.md</c>.
/// </summary>
public sealed class RecommendPersonalItemsTool : IAiTool
{
    private readonly IRecommendationService _recommendations;

    public RecommendPersonalItemsTool(IRecommendationService recommendations) => _recommendations = recommendations;

    public string Name => "recommend_personal_items";

    public string Description =>
        "Suggest feng shui items the user CARRIES OR WEARS - bracelet, necklace/pendant, ring, keychain, " +
        "wallet charm, car hanger. Scored against the user's own destiny elements (Tứ Trụ favorable elements " +
        "when their birth time is known, otherwise their Nạp Âm element), NOT against any room, so no " +
        "workspace is needed and no placement direction applies. " +
        "For items PLACED IN A ROOM (desk decor, plants, statues) use recommend_products instead. " +
        "Needs the user's date of birth on file - if the tool reports it missing, ask them for it.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["topN"] = new("integer", $"Desired number of suggestions ({ToolTopN.Min}..{ToolTopN.Max}, default {ToolTopN.Default})."),
        ["aspiration"] = new("string", "The feng shui GOAL the user states for this session (\"I want something for wealth\", " +
            "\"for my health\"). Only products an admin approved for that goal are considered, and the placement hint " +
            "points at the matching Bat Trach direction. Omit when they don't mention a goal; if their goal seems " +
            "relevant but unclear, ask them which one.",
            Enum: new[] { "Wealth", "Career", "Health", "Relationship", "Study" }),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var request = new GeneratePersonalRecommendationRequest
        {
            TopN = ToolTopN.Clamp(ToolArgs.GetInt(arguments, "topN")),
            Aspiration = ToolArgs.GetEnum<Domain.Enums.Catalog.Aspiration>(arguments, "aspiration"),
        };

        var result = await _recommendations.GeneratePersonalAsync(context.UserId, request, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Could not generate suggestions.");

        var data = result.Data;

        // Registry per-turn để AiChatService auto-link tên sản phẩm trong câu trả lời cuối.
        foreach (var it in data.Items)
            context.Products.Add(new AiProductRef(it.ProductId, it.ProductName));

        return ToolArgs.Json(new
        {
            RecommendationId = data.Id,
            data.CustomerElement,
            data.KuaNumber,
            data.KuaGroup,
            data.Status,
            // Căn cứ đã dùng (TuTru/NapAm + các hành cần bồi) — nói đúng cái này, đừng tự suy luận lại.
            data.PersonalTarget,
            // Non-null khi engine phải BỎ bộ lọc mục tiêu (chưa sản phẩm nào được duyệt thẻ đó) — phải nói lại cho user.
            EngineNote = data.Note,
            Items = data.Items.Select(i => new
            {
                i.ProductId,
                i.ProductName,
                Link = $"[{i.ProductName}](/products/{i.ProductId})",
                i.Price,
                i.Score,
                i.Rank,
                i.MatchFacts,
                i.CautionFacts,
                i.PlacementHint,
            }),
            Note = "These items travel with the person - do NOT give compass placement advice for them. "
                + "Explain the fit using 'personalTarget' and each item's matchFacts. When mentioning a product, "
                + "write its name EXACTLY as the 'link' value (a markdown link). Do not invent other URLs.",
        });
    }
}
