namespace FengDeskAI.Domain.Enums.Vendor;

/// <summary>
/// Trạng thái lời mời nhân viên cho garden store.
/// State machine: Pending → Accepted (staff đồng ý) → Revoked (owner gỡ); Pending → Rejected (staff từ chối); Pending → Revoked (owner huỷ lời mời).
/// Quyền store-scoped chỉ tính khi <see cref="Accepted"/>.
/// </summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Revoked = 3,
}
