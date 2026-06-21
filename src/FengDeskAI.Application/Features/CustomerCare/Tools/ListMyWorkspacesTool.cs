using System.Text.Json;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Liệt kê hồ sơ không gian của CHÍNH user (scope theo userId).</summary>
public sealed class ListMyWorkspacesTool : IAiTool
{
    private readonly IWorkspaceProfileService _workspaces;

    public ListMyWorkspacesTool(IWorkspaceProfileService workspaces) => _workspaces = workspaces;

    public string Name => "list_my_workspaces";
    public string Description => "List the current user's workspace profiles (id, name, style...). Use this id for recommend_products.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var result = await _workspaces.GetMineAsync(context.UserId, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Could not load the profiles.");

        return ToolArgs.Json(result.Data);
    }
}
