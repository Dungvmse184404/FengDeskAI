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
    public string Description => "Get the current user's account information (name, email, phone, role, date of birth, birthTime, gender) " +
        "PLUS their pre-computed feng shui profile under 'fengShui' (element/mệnh, kuaNumber, kuaGroup, favorableDirections). " +
        "Always use these provided feng shui values directly — never calculate mệnh/cung/directions yourself. " +
        "For a DEEPER reading of the current user (Bát Trạch directions with cung names, Tứ Trụ/Bát Tự four pillars), " +
        "pass their dateOfBirth + gender + birthTime (if present) to the compute_destiny_chart tool.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        var result = await _auth.GetMeAsync(context.UserId, ct);
        if (!result.IsSuccess || result.Data is null)
            return ToolArgs.Error(result.Message ?? "Could not load the account information.");

        return ToolArgs.Json(result.Data);
    }
}
