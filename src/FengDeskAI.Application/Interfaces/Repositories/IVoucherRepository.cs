using FengDeskAI.Domain.Entities.Promotion;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IVoucherRepository : IGenericRepository<Voucher>
{
    Task<Voucher?> GetByCodeAsync(string normalizedCode, CancellationToken ct = default);

    /// <summary>Voucher tự áp đang bật (chưa lọc thời hạn/điều kiện — việc của calculator).</summary>
    Task<List<Voucher>> GetActiveAutoApplyAsync(CancellationToken ct = default);

    /// <summary>Voucher đang bật và còn trong thời hạn — để khách xem.</summary>
    Task<List<Voucher>> GetAvailableAsync(DateTime nowUtc, CancellationToken ct = default);

    Task<(List<Voucher> Items, int Total)> GetPagedAsync(int skip, int take, CancellationToken ct = default);

    /// <summary>Số lượt người này đang giữ (đơn chưa hủy) của một voucher.</summary>
    Task<int> CountActiveRedemptionsAsync(Guid voucherId, Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Giữ một lượt: <c>UPDATE … SET used_count = used_count + 1 WHERE … AND used_count &lt; usage_limit</c>
    /// trong MỘT câu — hai khách tranh lượt cuối thì đúng một người được. false = đã hết lượt.
    /// </summary>
    Task<bool> TryIncrementUsageAsync(Guid voucherId, CancellationToken ct = default);

    /// <summary>Trả một lượt (không xuống dưới 0).</summary>
    Task DecrementUsageAsync(Guid voucherId, CancellationToken ct = default);

    /// <summary>Tăng lượt KHÔNG xét giới hạn — chỉ dùng khi khôi phục đơn đã trả tiền với giá đã giảm.</summary>
    Task ForceIncrementUsageAsync(Guid voucherId, CancellationToken ct = default);

    Task AddRedemptionAsync(VoucherRedemption redemption, CancellationToken ct = default);

    /// <summary>Lượt dùng của một đơn (tracked), bất kể trạng thái.</summary>
    Task<VoucherRedemption?> GetRedemptionByOrderAsync(Guid orderId, CancellationToken ct = default);
}
