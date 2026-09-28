using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Enums.Promotion;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Voucher mặc định của sàn. Chỉ chèn khi CHƯA có mã — Manager đã sửa/tắt thì seeder không ghi đè
/// (đổi điều kiện của mã đang chạy phải đi qua API hoặc data migration).
/// </summary>
public sealed class VoucherSeeder : IDataSeeder
{
    public const string FreeShip500Code = "FREESHIP500";

    private readonly AppDbContext _context;
    private readonly ILogger<VoucherSeeder> _logger;

    public VoucherSeeder(AppDbContext context, ILogger<VoucherSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 30;
    public string Name => "Voucher mặc định (FREESHIP500)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // IgnoreQueryFilters: mã đã xoá mềm vẫn chiếm unique index — chèn lại sẽ vỡ.
        if (await _context.Vouchers.IgnoreQueryFilters().AnyAsync(v => v.Code == FreeShip500Code, ct)) return;

        await _context.Vouchers.AddAsync(new Voucher
        {
            Code = FreeShip500Code,
            Name = "Miễn phí vận chuyển cho đơn từ 500.000đ",
            Description = "Tự áp khi tổng tiền hàng từ 500.000đ. Mỗi cửa hàng trong đơn được giảm tối đa bằng phí vận chuyển "
                          + "của cửa hàng đó và không vượt 8% tiền hàng của cửa hàng.",
            Type = VoucherType.FreeShipping,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = 500_000m,
            IsAutoApply = true,
            IsActive = true,
        }, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded voucher {Code}.", FreeShip500Code);
    }
}
