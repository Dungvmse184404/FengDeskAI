using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Chat;

/// <summary>
/// Quyền của một người dùng (<see cref="GranterUserId"/>) cho phép trợ lý AI tiết lộ thông tin cá nhân
/// của họ cho nhân viên hỗ trợ TRONG cùng phòng (<see cref="ChatboxId"/>). Mặc định tất cả = false.
/// Tool <c>get_chat_partner_info</c> CHỈ trả về scope được bật ở đây — enforcement ở tầng code.
/// Thiết kế mở rộng: thêm scope mới = thêm cột bool.
/// </summary>
public class ChatRoomDataConsent : BaseEntity
{
    public Guid ChatboxId { get; set; }
    public Guid GranterUserId { get; set; }

    /// <summary>Cho xem hồ sơ cá nhân (tên, ngày sinh, mệnh...). Mặc định bật (opt-out).</summary>
    public bool ShareProfile { get; set; } = true;

    /// <summary>Cho xem các hồ sơ không gian làm việc. Mặc định bật (opt-out).</summary>
    public bool ShareWorkspaces { get; set; } = true;

    /// <summary>Cho xem lịch sử đơn hàng. Mặc định bật (opt-out).</summary>
    public bool ShareOrders { get; set; } = true;

    public Chatbox Chatbox { get; set; } = null!;
}
