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
    public string Description => "Tạo gợi ý sản phẩm hợp phong thủy cho một hồ sơ không gian (lấy workspaceProfileId từ list_my_workspaces). Trả về danh sách kèm điểm + lý do.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>
    {
        ["workspaceProfileId"] = new("string", "Id (GUID) hồ sơ không gian của người dùng.", Required: true),
        ["topN"] = new("integer", "Số gợi ý mong muốn (mặc định 8, tối đa 20)."),
    };

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var profileId = ToolArgs.GetGuid(arguments, "workspaceProfileId");
        if (profileId is null)
            return ToolArgs.Error("Thiếu hoặc sai 'workspaceProfileId' (phải là GUID).");

        var request = new GenerateRecommendationRequest
        {
            WorkspaceProfileId = profileId.Value,
            TopN = ToolArgs.GetInt(arguments, "topN"),
        };
        var result = await _recommendations.GenerateAsync(context.UserId, request, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Không tạo được gợi ý.");

        return ToolArgs.Json(result.Data);
    }
}
