using System.Text.RegularExpressions;
using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Promotion.DTOs;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Promotion;

namespace FengDeskAI.Application.Features.Promotion.Services;

/// <summary>Voucher được chọn cho một đơn. <see cref="Error"/> chỉ có khi khách TỰ nhập mã mà mã không dùng được.</summary>
public sealed record VoucherSelection(Voucher? Voucher, VoucherQuote? Quote, string? Error)
{
    public static readonly VoucherSelection None = new(null, null, null);
    public decimal DiscountFor(Guid storeId) => Quote?.DiscountByStore.GetValueOrDefault(storeId) ?? 0m;
    public decimal TotalDiscount => Quote?.TotalDiscount ?? 0m;
}

public interface IVoucherService
{
    /// <summary>
    /// Chọn voucher cho đơn: khách nhập mã thì xét đúng mã đó (không dùng được ⇒ <c>Error</c>); không nhập thì
    /// lấy voucher tự áp giảm nhiều nhất mà đơn đủ điều kiện.
    /// </summary>
    Task<VoucherSelection> SelectAsync(Guid customerId, string? code, IReadOnlyList<StoreChargeInput> stores,
        Guid? destinationProvinceId, CancellationToken ct = default);

    /// <summary>Giữ một lượt cho đơn (gọi TRONG transaction checkout). false = vừa hết lượt.</summary>
    Task<bool> TryRedeemAsync(VoucherSelection selection, Order order, CancellationToken ct = default);

    /// <summary>Đơn hủy/hết hạn — trả lượt. Không có lượt nào thì thôi.</summary>
    Task ReleaseForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Đơn đã hết hạn nhưng tiền (giá đã giảm) vẫn về — giữ lại lượt, không xét giới hạn.</summary>
    Task ReinstateForOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<IServiceResult<List<VoucherResponse>>> GetAvailableAsync(CancellationToken ct = default);
    Task<IServiceResult<PagedResult<VoucherResponse>>> GetPagedAsync(PageRequest page, CancellationToken ct = default);
    Task<IServiceResult<VoucherResponse>> CreateAsync(CreateVoucherRequest request, CancellationToken ct = default);
    Task<IServiceResult<VoucherResponse>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}

public partial class VoucherService : IVoucherService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public VoucherService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    public async Task<VoucherSelection> SelectAsync(Guid customerId, string? code, IReadOnlyList<StoreChargeInput> stores,
        Guid? destinationProvinceId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Đơn mới sẽ chốt đúng tỉ lệ này vào delivery ⇒ trần giảm tính theo nó.
        var rate = PlatformFeePolicy.CommissionRate;

        if (!string.IsNullOrWhiteSpace(code))
        {
            var voucher = await _uow.Vouchers.GetByCodeAsync(NormalizeCode(code), ct);
            if (voucher is null) return new VoucherSelection(null, null, "Mã giảm giá không tồn tại.");

            var limitError = await CheckLimitsAsync(voucher, customerId, ct);
            if (limitError is not null) return new VoucherSelection(voucher, null, limitError);

            var quote = ShippingVoucherCalculator.Quote(voucher, stores, destinationProvinceId, now, rate);
            return quote.IsEligible
                ? new VoucherSelection(voucher, quote, null)
                : new VoucherSelection(voucher, null, quote.RejectReason);
        }

        VoucherSelection best = VoucherSelection.None;
        foreach (var voucher in await _uow.Vouchers.GetActiveAutoApplyAsync(ct))
        {
            if (await CheckLimitsAsync(voucher, customerId, ct) is not null) continue;
            var quote = ShippingVoucherCalculator.Quote(voucher, stores, destinationProvinceId, now, rate);
            if (quote.IsEligible && quote.TotalDiscount > best.TotalDiscount)
                best = new VoucherSelection(voucher, quote, null);
        }
        return best;
    }

    public async Task<bool> TryRedeemAsync(VoucherSelection selection, Order order, CancellationToken ct = default)
    {
        if (selection.Voucher is null || selection.TotalDiscount <= 0m) return true;
        if (!await _uow.Vouchers.TryIncrementUsageAsync(selection.Voucher.Id, ct)) return false;

        await _uow.Vouchers.AddRedemptionAsync(new VoucherRedemption
        {
            VoucherId = selection.Voucher.Id,
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            DiscountAmount = selection.TotalDiscount,
        }, ct);
        return true;
    }

    public async Task ReleaseForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var redemption = await _uow.Vouchers.GetRedemptionByOrderAsync(orderId, ct);
        if (redemption is not { Status: VoucherRedemptionStatus.Applied }) return;
        redemption.Status = VoucherRedemptionStatus.Released;
        await _uow.Vouchers.DecrementUsageAsync(redemption.VoucherId, ct);
    }

    public async Task ReinstateForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var redemption = await _uow.Vouchers.GetRedemptionByOrderAsync(orderId, ct);
        if (redemption is not { Status: VoucherRedemptionStatus.Released }) return;
        redemption.Status = VoucherRedemptionStatus.Applied;
        await _uow.Vouchers.ForceIncrementUsageAsync(redemption.VoucherId, ct);
    }

    public async Task<IServiceResult<List<VoucherResponse>>> GetAvailableAsync(CancellationToken ct = default)
        => ServiceResult<List<VoucherResponse>>.Success(
            _mapper.Map<List<VoucherResponse>>(await _uow.Vouchers.GetAvailableAsync(DateTime.UtcNow, ct)));

    public async Task<IServiceResult<PagedResult<VoucherResponse>>> GetPagedAsync(PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Vouchers.GetPagedAsync(page.Skip, page.PageSize, ct);
        return ServiceResult<PagedResult<VoucherResponse>>.Success(
            new PagedResult<VoucherResponse>(_mapper.Map<List<VoucherResponse>>(items), page.Page, page.PageSize, total));
    }

    public async Task<IServiceResult<VoucherResponse>> CreateAsync(CreateVoucherRequest request, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error is not null) return ServiceResult<VoucherResponse>.Failure(ApiStatusCodes.BadRequest, error);

        var code = NormalizeCode(request.Code!);
        if (await _uow.Vouchers.GetByCodeAsync(code, ct) is not null)
            return ServiceResult<VoucherResponse>.Failure(ApiStatusCodes.Conflict, "Mã giảm giá đã tồn tại.");

        var voucher = new Voucher
        {
            Code = code,
            Name = request.Name!.Trim(),
            Description = request.Description?.Trim(),
            Type = VoucherType.FreeShipping,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = request.MinOrderSubtotal,
            MaxDiscountAmount = request.MaxDiscountAmount,
            ProvinceId = request.ProvinceId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            UsageLimit = request.UsageLimit,
            UsageLimitPerUser = request.UsageLimitPerUser,
            IsAutoApply = request.IsAutoApply,
            IsActive = true,
        };
        await _uow.Vouchers.AddAsync(voucher, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<VoucherResponse>.Success(_mapper.Map<VoucherResponse>(voucher));
    }

    public async Task<IServiceResult<VoucherResponse>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var voucher = await _uow.Vouchers.GetByIdAsync(id, ct);
        if (voucher is null) return ServiceResult<VoucherResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy mã giảm giá.");
        voucher.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<VoucherResponse>.Success(_mapper.Map<VoucherResponse>(voucher));
    }

    // ===================== Nội bộ =====================

    private async Task<string?> CheckLimitsAsync(Voucher voucher, Guid customerId, CancellationToken ct)
    {
        if (voucher.UsageLimit is { } limit && voucher.UsedCount >= limit)
            return "Mã giảm giá đã hết lượt sử dụng.";
        if (voucher.UsageLimitPerUser is { } perUser
            && await _uow.Vouchers.CountActiveRedemptionsAsync(voucher.Id, customerId, ct) >= perUser)
            return "Bạn đã dùng hết lượt của mã giảm giá này.";
        return null;
    }

    [GeneratedRegex("^[A-Z0-9_-]{3,50}$")]
    private static partial Regex CodePattern();

    private static string? Validate(CreateVoucherRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Code) || !CodePattern().IsMatch(NormalizeCode(r.Code)))
            return "Mã gồm 3–50 ký tự chữ, số, '-' hoặc '_'.";
        if (string.IsNullOrWhiteSpace(r.Name)) return "Tên mã giảm giá là bắt buộc.";
        if (r.MinOrderSubtotal < 0) return "Tiền hàng tối thiểu không được âm.";
        if (r.MaxDiscountAmount is <= 0) return "Mức giảm tối đa phải lớn hơn 0.";
        if (r.UsageLimit is <= 0 || r.UsageLimitPerUser is <= 0) return "Giới hạn lượt dùng phải lớn hơn 0.";
        if (r.StartsAt is { } s && r.EndsAt is { } e && e <= s) return "Thời gian kết thúc phải sau thời gian bắt đầu.";
        return null;
    }
}
