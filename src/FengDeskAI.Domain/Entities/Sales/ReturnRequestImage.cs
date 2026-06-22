using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>Ảnh bằng chứng đính kèm yêu cầu trả hàng (hàng lỗi/hư hại...).</summary>
public class ReturnRequestImage : BaseEntity
{
    public Guid ReturnRequestId { get; set; }
    public string ImageUrl { get; set; } = null!;
    public int SortOrder { get; set; }

    public ReturnRequest ReturnRequest { get; set; } = null!;
}
