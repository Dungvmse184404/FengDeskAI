using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// 1 lần yêu cầu sinh/tạo lại model 3D cho 1 <see cref="Product"/> — hàng chờ + lịch sử (n–1 với Product).
/// <see cref="Model3DRequestType.Initial"/>: tự động, worker (<c>Model3DPollingWorker</c>) gọi Meshy, retry
/// khi hết credit (402). <see cref="Model3DRequestType.Regenerate"/>: thủ công, chỉ staff sàn
/// (<c>UserRole.Staff</c> trở lên) xử lý — không có bước "claim/khóa", bất kỳ staff nào cũng gọi
/// <c>generate</c>/<c>retry</c>/<c>accept</c> được.
///
/// Không giới hạn số request theo thời gian, nhưng chỉ 1 request "đang mở" tại 1 thời điểm cho mỗi
/// product — kiểm tra ở tầng service (<c>Model3DRequestService</c>/<c>ProductModel3DService</c>), không
/// phải constraint DB.
/// </summary>
public class Model3DRequest : BaseEntity
{
    public Guid ProductId { get; set; }

    public Model3DRequestType RequestType { get; set; }
    public Model3DRequestStatus Status { get; set; }

    /// <summary>User tạo request — garden owner hoặc garden staff (Accepted) của store sở hữu product.</summary>
    public Guid RequestedBy { get; set; }

    /// <summary>
    /// Ảnh nguồn cho lần thử gần nhất — 1 đến 4 <see cref="ProductImage"/> (giới hạn của Meshy
    /// multi-image-to-3d). Bị ghi đè mỗi khi staff <c>retry</c> với ảnh khác.
    /// </summary>
    public List<Guid> SourceImageIds { get; set; } = new();

    /// <summary>Task id Meshy của lần thử hiện tại — ghi đè mỗi lần <c>generate</c>/<c>retry</c>.</summary>
    public string? MeshyTaskId { get; set; }

    /// <summary>Staff đã gọi generate/retry gần nhất — chỉ để audit "ai làm gần nhất", không phải khóa độc quyền.</summary>
    public Guid? AssignedStaffId { get; set; }

    /// <summary>Lý do lỗi nội bộ — CHỈ trả cho staff sàn qua API, không map ra response cho owner/garden staff.</summary>
    public Model3DFailureReason? InternalFailureReason { get; set; }

    /// <summary>Mốc worker được thử lại — dùng khi <see cref="InternalFailureReason"/> = InsufficientCredits (backoff).</summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>Lý do staff từ chối xử lý (khi Status = Rejected).</summary>
    public string? RejectedReason { get; set; }

    public Product Product { get; set; } = null!;
}
