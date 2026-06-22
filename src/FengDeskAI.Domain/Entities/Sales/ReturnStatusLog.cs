using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>Nhật ký chuyển trạng thái của một yêu cầu trả hàng/đổi trả.</summary>
public class ReturnStatusLog : BaseEntity
{
    public Guid ReturnRequestId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public Guid? ChangedBy { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
}
