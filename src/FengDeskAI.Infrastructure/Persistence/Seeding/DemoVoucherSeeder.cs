using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Enums.Promotion;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Bộ mã giảm giá DEMO để bấm thử lúc trình bày — tách khỏi <see cref="VoucherSeeder"/> (mã mặc định của
/// sàn) để sau này muốn bỏ demo chỉ cần gỡ đúng seeder này.
///
/// <para>
/// GIỚI HẠN CẦN BIẾT: hệ thống hiện chỉ có <see cref="VoucherType.FreeShipping"/> do
/// <see cref="VoucherFundingSource.Platform"/> tài trợ, và mức giảm mỗi vườn bị chặn bởi
/// <c>min(phí ship của vườn, phí sàn thu trên vườn đó)</c> — xem <c>ShippingVoucherCalculator</c>.
/// Nghĩa là <b>đơn giá trị nhỏ gần như không giảm được bao nhiêu</b>: đơn 10.000đ với phí sàn 8% chỉ cho
/// trần giảm 800đ, không thể đưa phí ship về 0. Muốn có kịch bản "đơn 10k, ship 0đ, hoa hồng 0đ" thì phải
/// thêm loại voucher mới chứ không phải thêm mã — các mã dưới đây cố ý đặt ngưỡng cao để còn thấy tác dụng.
/// </para>
///
/// Chỉ chèn mã chưa tồn tại; Manager sửa/tắt rồi thì seeder không ghi đè.
/// </summary>
public sealed class DemoVoucherSeeder : IDataSeeder
{
    /// <summary>Mã demo — khai ở đây để test và tài liệu tham chiếu được, không gõ chuỗi rải rác.</summary>
    public const string FreeShipAllCode = "DEMOSHIP0";
    public const string FreeShipCapped20KCode = "DEMOSHIP20K";
    public const string FreeShipBigOrderCode = "DEMOSHIP1M";
    public const string FreeShipManualCode = "DEMOSHIPCODE";
    public const string PlatformDiscountCode = "DEMOSALEPLATFORM";
    public const string SellerDiscountCode = "DEMOSALESHOP";
    public const string FlatTotal10KCode = "DEMO10K";

    private readonly AppDbContext _context;
    private readonly ILogger<DemoVoucherSeeder> _logger;

    public DemoVoucherSeeder(AppDbContext context, ILogger<DemoVoucherSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Sau VoucherSeeder (30) để mã mặc định luôn vào trước.
    public int Order => 31;
    public string Name => "Voucher demo (DEMOSHIP* / DEMOSALE* / DEMO10K)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var demos = Build();
        var codes = demos.Select(v => v.Code).ToList();

        // IgnoreQueryFilters: mã đã xoá mềm vẫn chiếm unique index — chèn lại sẽ vỡ.
        var existing = await _context.Vouchers.IgnoreQueryFilters()
            .Where(v => codes.Contains(v.Code))
            .Select(v => v.Code)
            .ToListAsync(ct);

        var missing = demos.Where(v => !existing.Contains(v.Code)).ToList();
        if (missing.Count == 0) return;

        await _context.Vouchers.AddRangeAsync(missing, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} demo voucher(s): {Codes}.",
            missing.Count, string.Join(", ", missing.Select(v => v.Code)));
    }

    private static List<Voucher> Build() =>
    [
        // Ngưỡng thấp nhất còn có ý nghĩa: 200k tiền hàng → phí sàn 8% = 16k, đủ bù một phần phí ship.
        new Voucher
        {
            Code = FreeShipAllCode,
            Name = "[DEMO] Miễn phí vận chuyển cho đơn từ 200.000đ",
            Description = "Mã demo, nhập tay. Áp cho đơn từ 200.000đ. Mức giảm mỗi cửa hàng không vượt "
                          + "phí vận chuyển và cũng không vượt phí sàn thu trên cửa hàng đó.",
            Type = VoucherType.FreeShipping,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = 200_000m,
            // CỐ Ý không tự áp: mã demo được bật tự áp sẽ tự nhảy vào MỌI đơn đủ ngưỡng, đổi hành vi
            // mặc định của sàn và làm đỏ test VOUCHER-02. Mã demo luôn phải để khách nhập tay.
            IsAutoApply = false,
            IsActive = true,
        },

        // Có trần giảm → dùng để chỉ ra sự khác nhau giữa "trần của mã" và "trần theo phí sàn".
        new Voucher
        {
            Code = FreeShipCapped20KCode,
            Name = "[DEMO] Giảm phí ship tối đa 20.000đ",
            Description = "Mã demo có trần giảm 20.000đ trên cả đơn, dùng để thấy rõ khoản giảm bị chặn bởi "
                          + "trần của mã chứ không chỉ bởi phí sàn. Nhập tay, không tự áp.",
            Type = VoucherType.FreeShipping,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = 300_000m,
            MaxDiscountAmount = 20_000m,
            IsAutoApply = false,
            IsActive = true,
        },

        // Đơn lớn: phí sàn đủ lớn nên khoản giảm chạm trần thật là phí ship → ship về 0.
        new Voucher
        {
            Code = FreeShipBigOrderCode,
            Name = "[DEMO] Miễn phí vận chuyển cho đơn từ 1.000.000đ",
            Description = "Mã demo cho đơn lớn. Với đơn từ 1 triệu, phí sàn đủ lớn để bù trọn phí vận chuyển "
                          + "nên khách thấy phí ship về 0đ — đây là kịch bản đẹp nhất để trình bày.",
            Type = VoucherType.FreeShipping,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = 1_000_000m,
            IsAutoApply = false,
            IsActive = true,
        },

        // Giới hạn lượt dùng → để demo thông báo "mã đã hết lượt" mà không phải sửa DB tay.
        new Voucher
        {
            Code = FreeShipManualCode,
            Name = "[DEMO] Mã nhập tay, 50 lượt",
            Description = "Mã demo giới hạn 50 lượt toàn sàn và 1 lượt mỗi người, dùng để trình bày luồng "
                          + "kiểm tra giới hạn lượt dùng và thông báo khi hết lượt.",
            Type = VoucherType.FreeShipping,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = 150_000m,
            UsageLimit = 50,
            UsageLimitPerUser = 1,
            IsAutoApply = false,
            IsActive = true,
        },

        // Giảm TIỀN HÀNG do sàn chịu — trần = phí sàn của vườn, nhà vườn vẫn nhận đủ tiền hàng − phí sàn.
        new Voucher
        {
            Code = PlatformDiscountCode,
            Name = "[DEMO] Sàn giảm giá tiền hàng",
            Description = "Mã demo do SÀN tài trợ: trừ thẳng vào tiền hàng, mức giảm mỗi cửa hàng không vượt "
                          + "phí sàn thu trên cửa hàng đó. Doanh thu nhà vườn KHÔNG đổi.",
            Type = VoucherType.PlatformDiscount,
            FundedBy = VoucherFundingSource.Platform,
            MinOrderSubtotal = 100_000m,
            IsAutoApply = false,
            IsActive = true,
        },

        // Giảm TIỀN HÀNG do chính nhà vườn chịu — hoa hồng sàn vẫn tính trên tiền hàng GỐC.
        new Voucher
        {
            Code = SellerDiscountCode,
            Name = "[DEMO] Cửa hàng giảm giá, tối đa 50.000đ",
            Description = "Mã demo do NHÀ VƯỜN tài trợ: trừ thẳng vào tiền người bán nhận, hoa hồng sàn vẫn "
                          + "tính trên tiền hàng gốc. Dùng để thấy doanh thu người bán giảm đúng phần khuyến mãi.",
            Type = VoucherType.SellerDiscount,
            FundedBy = VoucherFundingSource.Seller,
            MinOrderSubtotal = 100_000m,
            MaxDiscountAmount = 50_000m,
            IsAutoApply = false,
            IsActive = true,
        },

        // Mã trình bày: kéo tổng đơn về đúng 10.000đ, trừ lần lượt ship → hoa hồng sàn → tiền người bán.
        new Voucher
        {
            Code = FlatTotal10KCode,
            Name = "[DEMO] Tổng đơn còn 10.000đ",
            Description = "Mã CHỈ ĐỂ TRÌNH BÀY: kéo tổng tiền khách phải trả về đúng 10.000đ. Trừ lần lượt phí "
                          + "vận chuyển, rồi hoa hồng sàn, hết mới tới tiền người bán — xem được cả ba khoản "
                          + "cùng lúc trên một đơn.",
            Type = VoucherType.DemoFlatTotal,
            FundedBy = VoucherFundingSource.Mixed,
            MinOrderSubtotal = 0m,
            IsAutoApply = false,
            IsActive = true,
        },
    ];
}
