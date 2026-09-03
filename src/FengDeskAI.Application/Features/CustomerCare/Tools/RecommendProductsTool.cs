using System.Text.Json;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Gợi ý sản phẩm đặt trong KHÔNG GIAN cho 1 hồ sơ workspace của CHÍNH user (scope theo userId).</summary>
public sealed class RecommendProductsTool : IAiTool
{
    private readonly IRecommendationService _recommendations;
    private readonly IWorkspaceProfileService _workspaces;

    public RecommendProductsTool(IRecommendationService recommendations, IWorkspaceProfileService workspaces)
    {
        _recommendations = recommendations;
        _workspaces = workspaces;
    }

    public string Name => "recommend_products";

    public string Description =>
        "Suggest feng-shui-fitting products to PLACE IN A ROOM/WORKSPACE (desk items, decor, plants). " +
        "Scored against what the room's five elements are missing. Pass workspaceProfileId from " +
        "list_my_workspaces; omit it to use the user's default workspace. Returns a ranked list with " +
        "scores, reasons and placement hints. " +
        "For items the user CARRIES OR WEARS (bracelet, pendant, charm, car hanger, wallet item) use " +
        "recommend_personal_items instead — those are scored against the person, not a room.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["workspaceProfileId"] = new("string", "The user's workspace profile id (GUID) from list_my_workspaces. Omit to use their default workspace."),
        ["topN"] = new("integer", $"Desired number of suggestions ({ToolTopN.Min}..{ToolTopN.Max}, default {ToolTopN.Default})."),
        ["aspiration"] = new("string", "The feng shui GOAL the user states for this session (\"I want something for wealth\", " +
            "\"for my health\"). Only products an admin approved for that goal are considered, and the placement hint " +
            "points at the matching Bat Trach direction. Omit when they don't mention a goal; if their goal seems " +
            "relevant but unclear, ask them which one.",
            Enum: new[] { "Wealth", "Career", "Health", "Relationship", "Study" }),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        // Model hay quên gọi list_my_workspaces trước — tự rơi về workspace mặc định thay vì báo lỗi.
        var profileId = ToolArgs.GetGuid(arguments, "workspaceProfileId");
        if (profileId is null)
        {
            var mine = await _workspaces.GetMineAsync(context.UserId, ct);
            var fallback = mine.Data?.FirstOrDefault(w => w.IsDefault) ?? mine.Data?.FirstOrDefault();
            if (fallback is null)
                return ToolArgs.Error(
                    "The user has no workspace profile yet. Ask them to create one (or call list_my_workspaces to confirm), "
                    + "then retry with its id.");
            profileId = fallback.Id;
        }

        var request = new GenerateRecommendationRequest
        {
            WorkspaceProfileId = profileId.Value,
            TopN = ToolTopN.Clamp(ToolArgs.GetInt(arguments, "topN")),
            Aspiration = ToolArgs.GetEnum<Domain.Enums.Catalog.Aspiration>(arguments, "aspiration"),
        };
        var result = await _recommendations.GenerateAsync(context.UserId, request, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Could not generate suggestions.");

        var data = result.Data;

        // Ghi registry để AiChatService auto-link tên sản phẩm trong câu trả lời cuối (nếu model quên).
        foreach (var it in data.Items)
            context.Products.Add(new AiProductRef(it.ProductId, it.ProductName));

        // Trả kèm sẵn field "link" (markdown) cho từng sản phẩm — model chỉ việc dùng nguyên văn.
        // KHÔNG trả personalWeight: field legacy engine v2, model đọc được sẽ diễn giải sai.
        return ToolArgs.Json(new
        {
            RecommendationId = data.Id,
            WorkspaceProfileId = profileId.Value,
            data.CustomerElement,
            data.KuaNumber,
            data.KuaGroup,
            data.Status,
            data.Summary,
            data.Note,
            data.Gap,
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
                i.Explanation,
            }),
            //Note = "When mentioning any of these products in your reply, write the product name EXACTLY as the 'link' value (a markdown link). Do not invent other URLs.",
        });
    }
}
