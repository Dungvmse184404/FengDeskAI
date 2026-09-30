using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Domain.Entities.Payment;

/// <summary>
/// Một bút toán sổ cái — CHỈ THÊM, không sửa, không xóa. Số dư của một sổ = Σ <see cref="Amount"/>.
///
/// <para>
/// <see cref="IdempotencyKey"/> là khoá duy nhất ở DB (vd <c>garden:SaleCredit:delivery:{id}</c>): webhook
/// lặp, worker quét lại hay hai instance chạy song song cũng không thể ghi một khoản hai lần. Đây chính là
/// thứ thay cho cờ <c>deliveries.payout_credited_at</c> — ba lỗi trong docs/adr/vendor-payout.md §3b đều do
/// thiếu khoá kiểu này.
/// </para>
/// </summary>
public class LedgerEntry : BaseEntity
{
    public LedgerAccount Account { get; set; }

    /// <summary>Bắt buộc với sổ nhà vườn; null với sổ sàn.</summary>
    public Guid? GardenStoreId { get; set; }

    public LedgerEntryType Type { get; set; }

    /// <summary>Có dấu: + tiền vào sổ, − tiền ra khỏi sổ.</summary>
    public decimal Amount { get; set; }

    /// <summary>Từ lúc nào khoản này được tính là "có thể chi". Khoản trừ có hiệu lực ngay.</summary>
    public DateTime AvailableAt { get; set; }

    public Guid? OrderId { get; set; }
    public Guid? DeliveryId { get; set; }
    public Guid? RefundId { get; set; }
    public Guid? VendorLiabilityId { get; set; }

    public string IdempotencyKey { get; set; } = null!;
    public string? Note { get; set; }
}
