using FengDeskAI.Domain.Entities.Payment;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>Một dòng lịch sử phí sàn kèm tên người đổi (null = hệ thống/migration).</summary>
public sealed record PlatformFeeRateHistoryRow(
    Guid Id, decimal CommissionRate, DateTime EffectiveFrom, string? Note, DateTime CreatedAt, string? ChangedByName);

public interface IPlatformFeeRateRepository : IGenericRepository<PlatformFeeRate>
{
    /// <summary>Dòng đang áp tại <paramref name="nowUtc"/> (EffectiveFrom mới nhất ≤ now); null nếu bảng trống.</summary>
    Task<PlatformFeeRate?> GetEffectiveAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Lịch sử, mới nhất trước.</summary>
    Task<List<PlatformFeeRateHistoryRow>> GetHistoryAsync(int take, CancellationToken ct = default);
}
