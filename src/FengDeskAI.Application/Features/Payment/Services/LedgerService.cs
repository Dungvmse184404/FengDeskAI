using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Payment;

namespace FengDeskAI.Application.Features.Payment.Services;

/// <summary>
/// Nơi DUY NHẤT ghi sổ cái. Mọi hàm đều idempotent theo khoá bút toán: gọi lại (webhook lặp, worker quét lại,
/// backfill chạy nhiều lần) không sinh thêm tiền. Hàm chỉ thêm bút toán vào UnitOfWork — người gọi SaveChanges
/// trong cùng transaction với thay đổi nghiệp vụ, để trạng thái và tiền không bao giờ lệch nhau.
///
/// Bảng dấu của từng sự kiện: docs/adr/platform-fee-ledger.md §3.
/// </summary>
public interface ILedgerService
{
    /// <summary>Delivery vừa sang Delivered: tiền hàng cho vườn, phí sàn, phí ship thu/chi của sàn.</summary>
    Task PostDeliveryCompletedAsync(Delivery delivery, CancellationToken ct = default);

    /// <summary>Refund Completed: sàn đã trả tiền cho khách.</summary>
    Task PostRefundPaidOutAsync(Refund refund, Guid gardenStoreId, CancellationToken ct = default);

    /// <summary>
    /// Công nợ vừa sinh: vườn chịu phần hàng bị hoàn, được trả lại phí sàn tương ứng. <paramref name="refundedDelivery"/>
    /// là delivery gốc bị hoàn — lấy tỉ lệ phí đã chốt và mốc hết giữ tiền của nó.
    /// </summary>
    Task PostLiabilityRaisedAsync(VendorLiability liability, Delivery refundedDelivery, CancellationToken ct = default);

    /// <summary>Manager miễn công nợ: đảo đúng hai bút toán lúc sinh công nợ.</summary>
    Task PostLiabilityWaivedAsync(VendorLiability liability, CancellationToken ct = default);
}

public class LedgerService : ILedgerService
{
    private readonly IUnitOfWork _uow;

    public LedgerService(IUnitOfWork uow) => _uow = uow;

    public Task PostDeliveryCompletedAsync(Delivery delivery, CancellationToken ct = default)
    {
        // Delivery đổi hàng (RMA) giá trị 0đ, gửi lại hàng đã bán — không phát sinh doanh thu mới.
        if (delivery.IsExchange) return Task.CompletedTask;

        var deliveredAt = delivery.DeliveredAt ?? DateTime.UtcNow;
        // Tiền của vườn chỉ khả dụng sau khoảng giữ = cửa sổ đổi trả (PayoutPolicy).
        var clearsAt = deliveredAt.AddDays(PayoutPolicy.HoldDays);
        var commission = PlatformFeePolicy.ComputeCommission(delivery.Subtotal, delivery.CommissionRate);
        var source = new Source(delivery.OrderId, delivery.Id);

        var entries = new List<LedgerEntry>
        {
            Garden(delivery.GardenStoreId, LedgerEntryType.SaleCredit, delivery.Subtotal, clearsAt, source, "delivery", delivery.Id),
            Garden(delivery.GardenStoreId, LedgerEntryType.Commission, -commission, clearsAt, source, "delivery", delivery.Id,
                $"Phí sàn {delivery.CommissionRate:P0}"),
            Platform(LedgerEntryType.Commission, commission, deliveredAt, source, "delivery", delivery.Id),
            Platform(LedgerEntryType.ShippingCollected, delivery.ShippingFee, deliveredAt, source, "delivery", delivery.Id),
            // Voucher sàn tài trợ: sàn chịu, sổ vườn không đổi (≤ phí sàn của delivery — ShippingVoucherCalculator).
            Platform(LedgerEntryType.ShippingVoucherSubsidy, -delivery.ShippingDiscount, deliveredAt, source, "delivery", delivery.Id),
            Platform(LedgerEntryType.ItemVoucherSubsidy, -delivery.PlatformItemDiscount, deliveredAt, source, "delivery", delivery.Id),
            // Voucher do CHÍNH nhà vườn tài trợ: trừ vào tiền họ nhận. Hoa hồng ở trên vẫn tính trên
            // Subtotal GỐC — người bán tự chịu phần khuyến mãi của mình, sàn không gánh hộ.
            Garden(delivery.GardenStoreId, LedgerEntryType.SellerVoucherDiscount, -delivery.SellerItemDiscount,
                clearsAt, source, "delivery", delivery.Id),
        };
        if (delivery.CarrierShippingFee is { } carrierFee)
            entries.Add(Platform(LedgerEntryType.CarrierShippingCost, -carrierFee, deliveredAt, source, "delivery", delivery.Id));

        return AddMissingAsync(entries, ct);
    }

    public Task PostRefundPaidOutAsync(Refund refund, Guid gardenStoreId, CancellationToken ct = default)
    {
        var at = refund.CompletedAt ?? DateTime.UtcNow;
        var source = new Source(refund.OrderId, null) { RefundId = refund.Id };
        return AddMissingAsync(
            [Platform(LedgerEntryType.RefundPaidOut, -refund.Amount, at, source, "refund", refund.Id, $"Hoàn tiền khách — vườn {gardenStoreId}")],
            ct);
    }

    public Task PostLiabilityRaisedAsync(VendorLiability liability, Delivery refundedDelivery, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var reversal = PlatformFeePolicy.ComputeCommission(liability.Amount, refundedDelivery.CommissionRate);
        var source = new Source(null, null) { RefundId = liability.RefundId, VendorLiabilityId = liability.Id };
        var garden = liability.GardenStoreId;

        // Khoản trừ có hiệu lực cùng lúc với tiền hàng mà nó triệt tiêu: hoàn hàng thường xảy ra TRONG khoảng giữ
        // (cửa sổ đổi trả = khoảng giữ), lúc đó tiền hàng còn "chờ" — trừ ngay vào "có thể chi" sẽ làm số đó âm
        // dù vườn không nợ gì. Tiền hàng đã qua khoảng giữ (đã có thể chi) thì trừ ngay.
        var effectiveAt = ClearsAt(refundedDelivery) is { } clearsAt && clearsAt > now ? clearsAt : now;
        return AddMissingAsync(
        [
            Garden(garden, LedgerEntryType.RefundLiability, -liability.Amount, effectiveAt, source, "liability", liability.Id),
            Platform(LedgerEntryType.RefundLiability, liability.Amount, now, source, "liability", liability.Id),
            Garden(garden, LedgerEntryType.CommissionReversal, reversal, effectiveAt, source, "liability", liability.Id),
            Platform(LedgerEntryType.CommissionReversal, -reversal, now, source, "liability", liability.Id),
        ], ct);
    }

    public async Task PostLiabilityWaivedAsync(VendorLiability liability, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var source = new Source(null, null) { RefundId = liability.RefundId, VendorLiabilityId = liability.Id };
        var garden = liability.GardenStoreId;

        // Đảo đúng số và đúng mốc đã ghi lúc sinh công nợ (tỉ lệ có thể đã đổi kể từ đó) — đọc lại từ sổ.
        var reversal = await _uow.Ledger.GetByKeyAsync(
            Key(LedgerAccount.GardenStore, LedgerEntryType.CommissionReversal, "liability", liability.Id), ct);
        var reinstated = reversal?.Amount ?? 0m;
        var effectiveAt = reversal is not null && reversal.AvailableAt > now ? reversal.AvailableAt : now;

        await AddMissingAsync(
        [
            Garden(garden, LedgerEntryType.LiabilityWaived, liability.Amount, effectiveAt, source, "liability", liability.Id),
            Platform(LedgerEntryType.LiabilityWaived, -liability.Amount, now, source, "liability", liability.Id),
            Garden(garden, LedgerEntryType.CommissionReinstated, -reinstated, effectiveAt, source, "liability", liability.Id),
            Platform(LedgerEntryType.CommissionReinstated, reinstated, now, source, "liability", liability.Id),
        ], ct);
    }

    // ===================== Nội bộ =====================

    /// <summary>Mốc tiền hàng của delivery hết bị giữ; null nếu delivery chưa giao.</summary>
    private static DateTime? ClearsAt(Delivery delivery) => delivery.DeliveredAt?.AddDays(PayoutPolicy.HoldDays);

    private sealed record Source(Guid? OrderId, Guid? DeliveryId)
    {
        public Guid? RefundId { get; init; }
        public Guid? VendorLiabilityId { get; init; }
    }

    /// <summary>Khoá bút toán: một (sổ, loại, nguồn) chỉ ghi được đúng một lần.</summary>
    public static string Key(LedgerAccount account, LedgerEntryType type, string sourceKind, Guid sourceId)
        => $"{account}:{type}:{sourceKind}:{sourceId:N}";

    private async Task AddMissingAsync(IReadOnlyList<LedgerEntry> entries, CancellationToken ct)
    {
        // Khoản 0đ (vd delivery không có phí ship) không đáng một dòng sổ.
        var candidates = entries.Where(e => e.Amount != 0m).ToList();
        if (candidates.Count == 0) return;

        var existing = await _uow.Ledger.GetExistingKeysAsync(candidates.Select(e => e.IdempotencyKey).ToList(), ct);
        var toAdd = candidates.Where(e => !existing.Contains(e.IdempotencyKey)).ToList();
        if (toAdd.Count > 0) await _uow.Ledger.AddRangeAsync(toAdd, ct);
    }

    private static LedgerEntry Garden(Guid gardenStoreId, LedgerEntryType type, decimal amount, DateTime availableAt,
        Source source, string sourceKind, Guid sourceId, string? note = null)
        => Build(LedgerAccount.GardenStore, gardenStoreId, type, amount, availableAt, source, sourceKind, sourceId, note);

    private static LedgerEntry Platform(LedgerEntryType type, decimal amount, DateTime availableAt,
        Source source, string sourceKind, Guid sourceId, string? note = null)
        => Build(LedgerAccount.Platform, null, type, amount, availableAt, source, sourceKind, sourceId, note);

    private static LedgerEntry Build(LedgerAccount account, Guid? gardenStoreId, LedgerEntryType type, decimal amount,
        DateTime availableAt, Source source, string sourceKind, Guid sourceId, string? note)
        => new()
        {
            Account = account,
            GardenStoreId = gardenStoreId,
            Type = type,
            Amount = amount,
            AvailableAt = availableAt,
            OrderId = source.OrderId,
            DeliveryId = source.DeliveryId,
            RefundId = source.RefundId,
            VendorLiabilityId = source.VendorLiabilityId,
            IdempotencyKey = Key(account, type, sourceKind, sourceId),
            Note = note,
        };
}
