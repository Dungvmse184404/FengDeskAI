namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>
/// Luồng chuẩn: AwaitingStaff → InProgress → Succeeded/Failed/Rejected.
/// Queued/Processing chỉ được giữ để đọc dữ liệu legacy trước migration.
/// </summary>
public enum Model3DRequestStatus
{
    /// <summary>Trạng thái legacy của Initial trước khi chuẩn hóa hàng chờ.</summary>
    Queued = 0,

    /// <summary>Trạng thái legacy; tương đương InProgress.</summary>
    Processing = 1,

    /// <summary>Đã tạo, chờ staff sàn xử lý.</summary>
    AwaitingStaff = 2,

    /// <summary>Staff đã gửi task Meshy; đang chờ xem trước/chấp nhận.</summary>
    InProgress = 3,

    Succeeded = 4,
    Failed = 5,

    /// <summary>Staff từ chối xử lý.</summary>
    Rejected = 6,
}
