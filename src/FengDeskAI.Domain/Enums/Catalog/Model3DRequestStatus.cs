namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>
/// Trạng thái 1 <c>Model3DRequest</c>. Luồng Initial: Queued → Processing → Succeeded/Failed.
/// Luồng Regenerate: AwaitingStaff → InProgress (mang tính thông tin, không khóa) → Succeeded/Rejected.
/// </summary>
public enum Model3DRequestStatus
{
    /// <summary>(Initial) Chờ worker gọi Meshy — hoặc đang chờ retry do hết credit (xem NextAttemptAt).</summary>
    Queued = 0,

    /// <summary>(Initial) Đã gửi task cho Meshy, worker đang poll kết quả.</summary>
    Processing = 1,

    /// <summary>(Regenerate) Đã tạo, chờ staff sàn xử lý — hiển thị trong hàng chờ chung.</summary>
    AwaitingStaff = 2,

    /// <summary>(Regenerate) Staff đã bấm generate ít nhất 1 lần — chỉ mang tính thông tin, không khóa.</summary>
    InProgress = 3,

    Succeeded = 4,
    Failed = 5,

    /// <summary>(Regenerate) Staff từ chối xử lý.</summary>
    Rejected = 6,
}
