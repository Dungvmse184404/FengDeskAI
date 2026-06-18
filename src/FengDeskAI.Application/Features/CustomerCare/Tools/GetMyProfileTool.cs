using System.Text.Json;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>Lấy thông tin tài khoản của CHÍNH user (scope theo userId).</summary>
public sealed class GetMyProfileTool : IAiTool
{
    private readonly IAuthService _auth;

    public GetMyProfileTool(IAuthService auth) => _auth = auth;

    public string Name => "get_my_profile";
    public string Description => "Lấy thông tin tài khoản người dùng hiện tại (tên, email, SĐT, vai trò, ngày sinh, giới tính).";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var result = await _auth.GetMeAsync(context.UserId, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Không lấy được thông tin tài khoản.");

        return ToolArgs.Json(result.Data);
    }
}
