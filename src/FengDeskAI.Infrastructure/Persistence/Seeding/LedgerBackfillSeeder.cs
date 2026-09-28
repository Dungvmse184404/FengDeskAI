using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Ghi sổ cái cho dữ liệu có TRƯỚC khi có sổ (migration <c>AddLedgerAndPlatformFee</c>): delivery đã giao,
/// refund đã hoàn xong, công nợ đã sinh/đã miễn. Đi qua chính <see cref="ILedgerService"/> nên số và khoá
/// bút toán y hệt luồng thật; chạy lại bao nhiêu lần cũng không ghi trùng.
///
/// Delivery cũ mang <c>commission_rate = 0</c> (mặc định của migration): đơn đặt khi chưa có phí sàn thì
/// không bị thu phí hồi tố — vườn nhận đủ tiền hàng như đã cam kết lúc đó.
/// </summary>
public sealed class LedgerBackfillSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILedgerService _ledger;
    private readonly ILogger<LedgerBackfillSeeder> _logger;

    public LedgerBackfillSeeder(AppDbContext context, ILedgerService ledger, ILogger<LedgerBackfillSeeder> logger)
    {
        _context = context;
        _ledger = ledger;
        _logger = logger;
    }

    public int Order => 100; // chạy sau mọi seeder dữ liệu tham chiếu/demo
    public string Name => "Ledger backfill (dữ liệu trước khi có sổ cái)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var ledger = _context.Set<LedgerEntry>();

        var deliveries = await _context.Deliveries
            .Where(d => d.Status == DeliveryStatus.Delivered && !d.IsExchange
                        && !ledger.Any(e => e.DeliveryId == d.Id && e.Type == LedgerEntryType.SaleCredit))
            .ToListAsync(ct);
        foreach (var delivery in deliveries)
            await _ledger.PostDeliveryCompletedAsync(delivery, ct);

        var refunds = await _context.Refunds
            .Include(r => r.ReturnRequest).ThenInclude(t => t.Delivery)
            .Where(r => r.Status == RefundStatus.Completed
                        && !ledger.Any(e => e.RefundId == r.Id && e.Type == LedgerEntryType.RefundPaidOut))
            .ToListAsync(ct);
        foreach (var refund in refunds)
            await _ledger.PostRefundPaidOutAsync(refund, refund.ReturnRequest.Delivery.GardenStoreId, ct);

        var liabilities = await _context.VendorLiabilities
            .Include(l => l.ReturnRequest).ThenInclude(t => t.Delivery)
            .Where(l => !ledger.Any(e => e.VendorLiabilityId == l.Id && e.Type == LedgerEntryType.RefundLiability)
                        || (l.Status == VendorLiabilityStatus.Waived
                            && !ledger.Any(e => e.VendorLiabilityId == l.Id && e.Type == LedgerEntryType.LiabilityWaived)))
            .ToListAsync(ct);
        foreach (var liability in liabilities)
        {
            await _ledger.PostLiabilityRaisedAsync(liability, liability.ReturnRequest.Delivery, ct);
            // Lưu trước để bút toán đảo đọc được bút toán gốc qua khoá.
            await _context.SaveChangesAsync(ct);
            if (liability.Status == VendorLiabilityStatus.Waived)
                await _ledger.PostLiabilityWaivedAsync(liability, ct);
        }

        var written = await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Ledger backfill: {Deliveries} delivery, {Refunds} refund, {Liabilities} công nợ — {Rows} dòng lưu ở lượt cuối.",
            deliveries.Count, refunds.Count, liabilities.Count, written);
    }
}
