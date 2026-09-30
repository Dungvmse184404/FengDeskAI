using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using Microsoft.Extensions.Caching.Memory;

namespace FengDeskAI.Application.Features.Vendor.Services;

public interface IPlatformFeeService
{
    /// <summary>Tỉ lệ phí sàn áp cho đơn đặt NGAY BÂY GIỜ. Checkout đọc một lần rồi chốt vào đơn.</summary>
    Task<decimal> GetCurrentRateAsync(CancellationToken ct = default);

    Task<IServiceResult<PlatformFeePolicyResponse>> GetPolicyAsync(CancellationToken ct = default);
    Task<IServiceResult<List<PlatformFeeRateHistoryResponse>>> GetHistoryAsync(CancellationToken ct = default);
    Task<IServiceResult<PlatformFeePolicyResponse>> UpdateRateAsync(UpdatePlatformFeeRequest request, CancellationToken ct = default);
}

/// <summary>
/// Phí sàn cấu hình được (Manager trở lên), lưu dạng lịch sử chỉ-thêm ở <c>platform_fee_rates</c>.
/// Đọc có cache vì checkout / xem trước phí / trang nhập giá đều cần, mà mỗi lượt DB ~300ms. Đổi phí thì xoá cache
/// ngay trên instance này; TTL ngắn lo phần nhiều instance.
/// </summary>
public class PlatformFeeService : IPlatformFeeService
{
    private const string CacheKey = "platform-fee:current";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);
    private const int HistorySize = 50;

    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;

    public PlatformFeeService(IUnitOfWork uow, IMemoryCache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    public async Task<decimal> GetCurrentRateAsync(CancellationToken ct = default)
        => (await GetEffectiveAsync(ct)).Rate;

    public async Task<IServiceResult<PlatformFeePolicyResponse>> GetPolicyAsync(CancellationToken ct = default)
    {
        var (rate, effectiveFrom) = await GetEffectiveAsync(ct);
        return ServiceResult<PlatformFeePolicyResponse>.Success(PlatformFeePolicy.Describe(rate, effectiveFrom));
    }

    public async Task<IServiceResult<List<PlatformFeeRateHistoryResponse>>> GetHistoryAsync(CancellationToken ct = default)
    {
        var rows = await _uow.PlatformFeeRates.GetHistoryAsync(HistorySize, ct);
        return ServiceResult<List<PlatformFeeRateHistoryResponse>>.Success(rows.Select(r => new PlatformFeeRateHistoryResponse
        {
            Id = r.Id,
            CommissionRate = r.CommissionRate,
            EffectiveFrom = r.EffectiveFrom,
            Note = r.Note,
            ChangedByName = r.ChangedByName,
        }).ToList());
    }

    public async Task<IServiceResult<PlatformFeePolicyResponse>> UpdateRateAsync(UpdatePlatformFeeRequest request, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error is not null) return ServiceResult<PlatformFeePolicyResponse>.Failure(ApiStatusCodes.BadRequest, error);
        var rate = request.CommissionRate!.Value;

        // So với số thật trong DB, không qua cache.
        var current = await _uow.PlatformFeeRates.GetEffectiveAsync(DateTime.UtcNow, ct);
        if (current is not null && current.CommissionRate == rate)
            return ServiceResult<PlatformFeePolicyResponse>.Failure(ApiStatusCodes.Conflict, "Phí sàn mới trùng với mức đang áp dụng.");

        var entry = new PlatformFeeRate
        {
            CommissionRate = rate,
            EffectiveFrom = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        await _uow.PlatformFeeRates.AddAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);

        return ServiceResult<PlatformFeePolicyResponse>.Success(
            PlatformFeePolicy.Describe(entry.CommissionRate, entry.EffectiveFrom));
    }

    private static string? Validate(UpdatePlatformFeeRequest request)
    {
        if (request.CommissionRate is not { } rate) return "Thiếu mức phí sàn.";
        if (rate < 0m || rate > PlatformFeePolicy.MaxCommissionRate)
            return $"Phí sàn phải từ 0% đến {PlatformFeePolicy.MaxCommissionRate * 100:0}%.";
        // Cột numeric(5,4): 8,25% = 0.0825 — lẻ hơn thì DB làm tròn âm thầm, nên chặn từ đây.
        if (decimal.Round(rate, 4) != rate)
            return "Phí sàn chỉ nhận tối đa 2 chữ số thập phân theo phần trăm (vd 8,25%).";
        if (request.Note is { Length: > 500 }) return "Ghi chú tối đa 500 ký tự.";
        return null;
    }

    private async Task<(decimal Rate, DateTime? EffectiveFrom)> GetEffectiveAsync(CancellationToken ct)
        => await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            var row = await _uow.PlatformFeeRates.GetEffectiveAsync(DateTime.UtcNow, ct);
            return row is null
                ? (PlatformFeePolicy.DefaultCommissionRate, (DateTime?)null)
                : (row.CommissionRate, (DateTime?)row.EffectiveFrom);
        });
}
