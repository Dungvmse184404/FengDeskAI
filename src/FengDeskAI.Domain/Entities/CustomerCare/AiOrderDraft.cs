using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.CustomerCare;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Draft đơn hàng user đã chốt với trợ lý AI trong một phòng chat riêng (tối đa 1 draft Pending mỗi phòng).
/// Được nạp lại vào prompt mỗi lượt để AI không quên user đã chọn gì; <c>confirm_order</c> đọc draft này
/// để tạo đơn thật. v1: một sản phẩm (một variant) mỗi draft.
/// Các cột *Snapshot / tên / địa chỉ dạng text là ảnh chụp lúc prepare — chỉ để hiển thị cho AI,
/// giá/tồn kho luôn được kiểm tra lại khi confirm.
/// </summary>
public class AiOrderDraft : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ChatboxId { get; set; }

    public Guid ProductId { get; set; }
    public Guid ProductItemId { get; set; }
    public int Quantity { get; set; }
    public Guid ShippingAddressId { get; set; }

    /// <summary>Phương thức thanh toán user đã chọn (mặc định PayOS; COD chỉ khi user yêu cầu).</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayOS;

    public decimal UnitPriceSnapshot { get; set; }
    public decimal ShippingFeeSnapshot { get; set; }
    public decimal TotalAmountSnapshot { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public string AddressText { get; set; } = string.Empty;

    public AiOrderDraftStatus Status { get; set; } = AiOrderDraftStatus.Pending;

    /// <summary>Hết hạn = now + DraftTtlMinutes, đặt lại mỗi lần user sửa draft.</summary>
    public DateTime ExpiresAt { get; set; }
}
