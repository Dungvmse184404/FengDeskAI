using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Enums.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Vendor.Services;

/// <summary>Cộng tiền hàng đã qua khoảng giữ vào số dư chủ vườn.</summary>
public interface IPayoutCreditService
{
    /// <summary>Quét các delivery đã giao, quá <see cref="PayoutPolicy.HoldDays"/>, chưa cộng. Trả số đã cộng.</summary>
    Task<int> CreditMaturedDeliveriesAsync(CancellationToken ct = default);
}

/// <summary>
/// Bước 1 của luồng chi tiền (ADR <c>vendor-payout.md</c>): tiền đã "sạch" thì cộng thẳng vào
/// <c>users.balance</c> của **chủ vườn chính** (<c>garden_store_owners.is_primary</c>).
///
/// <para>
/// Cố ý CHƯA có sổ cái/lệnh rút — đây là bản đơn giản nhất chạy được. Hai điểm phải giữ đúng nếu sửa:
/// <list type="number">
/// <item><c>Delivery.PayoutCreditedAt</c> là khoá chống cộng hai lần; worker quét lại mỗi chu kỳ nên
/// thiếu mốc này là tiền nhân đôi sau mỗi lượt.</item>
/// <item>Vườn nhiều đồng sở hữu thì tiền vào **một** người (primary). Đây là giới hạn đã biết, không
/// phải chia đều — chia đều cần bảng phân bổ riêng, để dành cho bước sổ cái.</item>
/// </list>
/// </para>
/// </summary>
public class PayoutCreditService : IPayoutCreditService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<PayoutCreditService> _logger;

    public PayoutCreditService(IUnitOfWork uow, ILogger<PayoutCreditService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<int> CreditMaturedDeliveriesAsync(CancellationToken ct = default)
    {
        // Cố ý dừng ở đây thay vì gỡ đăng ký worker: gỡ thì lần bật lại phải nhớ nối lại đủ 3 chỗ, còn cờ
        // này nằm ngay cạnh lời giải thích vì sao tắt. Xem PayoutPolicy.CreditToBalanceEnabled.
        if (!PayoutPolicy.CreditToBalanceEnabled) return 0;

        var now = DateTime.UtcNow;
        var matured = await _uow.Stores.GetDeliveriesToCreditAsync(now.AddDays(-PayoutPolicy.HoldDays), ct);
        if (matured.Count == 0) return 0;

        int credited = 0;
        foreach (var group in matured.GroupBy(d => d.GardenStoreId))
        {
            var owner = await _uow.Stores.GetPrimaryOwnerAsync(group.Key, ct);
            if (owner is null)
            {
                // Vườn không có chủ chính (dữ liệu lệch) — bỏ qua chứ KHÔNG đánh dấu đã cộng, để khi
                // gắn lại chủ thì tiền vẫn vào. Ghi log vì đây là trạng thái không nên tồn tại.
                _logger.LogWarning("Vườn {StoreId} không có chủ chính, chưa cộng được {Count} đơn.",
                    group.Key, group.Count());
                continue;
            }

            foreach (var delivery in group)
            {
                owner.Balance += delivery.Subtotal;
                delivery.PayoutCreditedAt = now;
                credited++;
            }

            _logger.LogInformation("Cộng {Amount} vào số dư {UserId} (vườn {StoreId}, {Count} đơn).",
                group.Sum(d => d.Subtotal), owner.Id, group.Key, group.Count());
        }

        if (credited > 0) await _uow.SaveChangesAsync(ct);
        return credited;
    }
}
