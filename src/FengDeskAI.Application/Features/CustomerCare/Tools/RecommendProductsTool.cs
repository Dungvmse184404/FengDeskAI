using System.Text.Json;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Gợi ý sản phẩm phong thủy cho 1 hồ sơ không gian của CHÍNH user (scope theo userId).</summary>
public sealed class RecommendProductsTool : IAiTool
{
    private readonly IRecommendationService _recommendations;

    public RecommendProductsTool(IRecommendationService recommendations) => _recommendations = recommendations;

    public string Name => "recommend_products";
    public string Description => "Generate feng-shui-fitting product suggestions for a workspace profile (get workspaceProfileId from list_my_workspaces). Returns a list with scores + reasons.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["workspaceProfileId"] = new("string", "The user's workspace profile id (GUID).", Required: true),
        ["topN"] = new("integer", "Desired number of suggestions (default 8, max 20)."),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var profileId = ToolArgs.GetGuid(arguments, "workspaceProfileId");
        if (profileId is null)
            return ToolArgs.Error("Missing or invalid 'workspaceProfileId' (must be a GUID).");

        var request = new GenerateRecommendationRequest
        {
            WorkspaceProfileId = profileId.Value,
            TopN = ToolArgs.GetInt(arguments, "topN"),
        };
        var result = await _recommendations.GenerateAsync(context.UserId, request, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Could not generate suggestions.");

        return ToolArgs.Json(result.Data);
    }
}
