using System.Text.Json;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Enums.Chat;

namespace FengDeskAI.Application.Features.CustomerCare.Tools;

/// <summary>
/// [Nhân viên hỗ trợ] Đọc thông tin của KHÁCH trong phòng hỗ trợ — CHỈ những phần khách đã cho phép
/// (bảng ChatRoomDataConsent). Không tham số: tự suy "khách" = chủ phòng. Enforcement ở code: scope
/// chưa duyệt sẽ KHÔNG trả dữ liệu. Vô tác dụng ngoài phòng có khách + nhân viên.
/// </summary>
public sealed class GetChatPartnerInfoTool : IAiTool
{
    private const string Denied = "The customer has not allowed sharing this item.";

    private readonly IUnitOfWork _uow;
    private readonly IAuthService _auth;
    private readonly IWorkspaceProfileService _workspaces;
    private readonly IOrderService _orders;

    public GetChatPartnerInfoTool(
        IUnitOfWork uow, IAuthService auth, IWorkspaceProfileService workspaces, IOrderService orders)
    {
        _uow = uow;
        _auth = auth;
        _workspaces = workspaces;
        _orders = orders;
    }

    public string Name => "get_chat_partner_info";
    public string Description =>
        "Get the customer's information in the room (profile/mệnh, workspace, order history). " +
        "If there is no data, reply that it may be because the customer has not consented to share their personal information.";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        if (context.ChatboxId is not { } chatboxId)
            return ToolArgs.Error("Could not determine the chat room.");

        var room = await _uow.Chatboxes.GetWithParticipantsAsync(chatboxId, ct);
        if (room is null)
            return ToolArgs.Error("Room not found.");

        // Người gọi phải là nhân viên hỗ trợ trong phòng.
        var caller = room.Participants.FirstOrDefault(p => p.UserId == context.UserId);
        var callerIsStaff = caller is not null &&
            caller.ParticipantType is ParticipantType.Staff or ParticipantType.Manager or ParticipantType.Admin;
        if (!callerIsStaff)
            return ToolArgs.Error("This tool is only for support staff in a room that has a customer.");

        // "Khách" = chủ phòng (Owner) là người dùng thật khác người gọi.
        var granter = room.Participants.FirstOrDefault(p =>
            p.Role == ParticipantRole.Owner && p.UserId.HasValue && p.UserId != context.UserId);
        if (granter?.UserId is not { } granterId)
            return ToolArgs.Error("This room has no customer to look up.");

        var consent = await _uow.Chatboxes.GetConsentAsync(chatboxId, granterId, ct);

        object profile = Denied, workspaces = Denied, orders = Denied;

        // Mặc định CHIA SẺ (opt-out): chưa có bản ghi consent → coi như khách cho phép tất cả.
        if (consent?.ShareProfile ?? true)
        {
            var r = await _auth.GetMeAsync(granterId, ct);
            profile = r.IsSuccess && r.Data is not null ? r.Data : "Could not load the profile.";
        }
        if (consent?.ShareWorkspaces ?? true)
        {
            var r = await _workspaces.GetMineAsync(granterId, ct);
            workspaces = r.IsSuccess && r.Data is not null ? r.Data : "Could not load the workspaces.";
        }
        if (consent?.ShareOrders ?? true)
        {
            var r = await _orders.GetMineAsync(granterId, new PageRequest { Page = 1, PageSize = 5 }, ct);
            orders = r.IsSuccess && r.Data is not null
                ? new { total = r.Data.TotalCount, items = r.Data.Items }
                : (object)"Could not load the orders.";
        }

        return ToolArgs.Json(new { profile, workspaces, orders });
    }
}
