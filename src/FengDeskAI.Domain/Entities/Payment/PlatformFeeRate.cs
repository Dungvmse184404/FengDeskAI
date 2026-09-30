using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Payment;

/// <summary>
/// Một lần đặt tỉ lệ phí sàn — CHỈ THÊM, không sửa: tỉ lệ đang áp là dòng mới nhất có
/// <see cref="EffectiveFrom"/> ≤ hiện tại. Giữ đủ lịch sử để trả lời "đơn tháng trước tính phí bao nhiêu, ai đổi".
/// Đơn đã đặt KHÔNG bị ảnh hưởng: tỉ lệ được chốt vào <c>orders.commission_rate</c> / <c>deliveries.commission_rate</c>.
/// Người đổi = <see cref="BaseEntity.CreatedBy"/>.
/// </summary>
public class PlatformFeeRate : BaseEntity
{
    /// <summary>Tỉ lệ trên tiền hàng, vd 0.0800 = 8%.</summary>
    public decimal CommissionRate { get; set; }

    public DateTime EffectiveFrom { get; set; }

    /// <summary>Lý do thay đổi — hiện trong lịch sử.</summary>
    public string? Note { get; set; }
}
