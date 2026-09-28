namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>
/// Loại bút toán. Cùng một loại có thể xuất hiện ở cả hai sổ với dấu ngược nhau — vd <see cref="Commission"/>
/// là −X ở sổ nhà vườn và +X ở sổ sàn. Chi tiết dấu từng loại: docs/adr/platform-fee-ledger.md.
/// </summary>
public enum LedgerEntryType
{
    /// <summary>Tiền hàng của delivery đã giao — nhà vườn +Subtotal.</summary>
    SaleCredit,

    /// <summary>Phí sàn trên delivery đã giao — nhà vườn −, sàn +.</summary>
    Commission,

    /// <summary>Phí ship khách đã trả cho delivery — sàn +.</summary>
    ShippingCollected,

    /// <summary>Khoản giảm phí ship từ voucher sàn tài trợ — sàn −. Không đụng sổ nhà vườn.</summary>
    ShippingVoucherSubsidy,

    /// <summary>Phí nhà vận chuyển thực tính cho delivery (sàn ký hợp đồng GHN) — sàn −.</summary>
    CarrierShippingCost,

    /// <summary>Tiền sàn đã hoàn cho khách khi refund Completed — sàn −.</summary>
    RefundPaidOut,

    /// <summary>Công nợ hoàn hàng do vườn chịu — nhà vườn −, sàn + (thu lại phần đã ứng).</summary>
    RefundLiability,

    /// <summary>Trả lại phí sàn tương ứng phần hàng bị hoàn — nhà vườn +, sàn −.</summary>
    CommissionReversal,

    /// <summary>Manager miễn công nợ (vườn không có lỗi) — đảo <see cref="RefundLiability"/>.</summary>
    LiabilityWaived,

    /// <summary>Miễn công nợ thì giao dịch bán coi như đứng — thu lại phí sàn đã trả ở <see cref="CommissionReversal"/>.</summary>
    CommissionReinstated,
}
