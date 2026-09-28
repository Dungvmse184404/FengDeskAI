using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Số tiền của MỘT vườn trong một đơn, chốt lúc checkout: tiền hàng, phí ship khách trả, khoản giảm phí ship.
///
/// Vì sao cần: đơn PayOS chỉ sinh delivery khi webhook báo đã trả — trước đây phí/khoản giảm theo từng vườn
/// tính lúc checkout bị mất và phải chia lại theo tỉ lệ. Giờ webhook đọc đúng số đã chốt ở đây.
/// </summary>
public class OrderStoreCharge : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid GardenStoreId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal ShippingDiscount { get; set; }
}
