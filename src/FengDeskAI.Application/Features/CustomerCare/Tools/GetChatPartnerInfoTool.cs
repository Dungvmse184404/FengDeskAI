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
    private const string Denied = "Khách chưa cho phép chia sẻ mục này.";

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
        "lấy thông tin khách hàng trong phòng (hồ sơ/mệnh, không gian làm việc, lịch sử đơn) " +
        "Nếu không có dữ liệu, trả lời là: có thể do khách hàng chưa đồng thuận chia sẻ thông tin cá nhân của họ";

    public IReadOnlyDictionary<string, AiToolParameter> Parameters => new Dictionary<string, AiToolParameter>();

    public async Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default)
    {
        if (context.ChatboxId is not { } chatboxId)
            return ToolArgs.Error("Không xác định được phòng chat.");

        var room = await _uow.Chatboxes.GetWithParticipantsAsync(chatboxId, ct);
        if (room is null)
            return ToolArgs.Error("Không tìm thấy phòng.");

        // Người gọi phải là nhân viên hỗ trợ trong phòng.
        var caller = room.Participants.FirstOrDefault(p => p.UserId == context.UserId);
        var callerIsStaff = caller is not null &&
            caller.ParticipantType is ParticipantType.Staff or ParticipantType.Manager or ParticipantType.Admin;
        if (!callerIsStaff)
            return ToolArgs.Error("Công cụ này chỉ dùng cho nhân viên hỗ trợ trong phòng có khách.");

        // "Khách" = chủ phòng (Owner) là người dùng thật khác người gọi.
        var granter = room.Participants.FirstOrDefault(p =>
            p.Role == ParticipantRole.Owner && p.UserId.HasValue && p.UserId != context.UserId);
        if (granter?.UserId is not { } granterId)
            return ToolArgs.Error("Phòng này không có khách để tra cứu.");

        var consent = await _uow.Chatboxes.GetConsentAsync(chatboxId, granterId, ct);

        object profile = Denied, workspaces = Denied, orders = Denied;

        // Mặc định CHIA SẺ (opt-out): chưa có bản ghi consent → coi như khách cho phép tất cả.
        if (consent?.ShareProfile ?? true)
        {
            var r = await _auth.GetMeAsync(granterId, ct);
            profile = r.IsSuccess && r.Data is not null ? r.Data : "Không lấy được hồ sơ.";
        }
        if (consent?.ShareWorkspaces ?? true)
        {
            var r = await _workspaces.GetMineAsync(granterId, ct);
            workspaces = r.IsSuccess && r.Data is not null ? r.Data : "Không lấy được không gian.";
        }
        if (consent?.ShareOrders ?? true)
        {
            var r = await _orders.GetMineAsync(granterId, new PageRequest { Page = 1, PageSize = 5 }, ct);
            orders = r.IsSuccess && r.Data is not null
                ? new { total = r.Data.TotalCount, items = r.Data.Items }
                : (object)"Không lấy được đơn hàng.";
        }

        return ToolArgs.Json(new { profile, workspaces, orders });
    }
}
