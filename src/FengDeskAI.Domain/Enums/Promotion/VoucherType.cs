namespace FengDeskAI.Domain.Enums.Promotion;

/// <summary>
/// Loại giảm giá — quyết định khoản giảm trừ vào ĐÂU và trần tối đa là bao nhiêu.
/// Thêm loại mới BẮT BUỘC thêm nhánh tính ở <c>VoucherDiscountCalculator</c>, nếu không mã sẽ bị từ chối.
/// </summary>
public enum VoucherType
{
    /// <summary>Giảm phí vận chuyển, sàn chịu. Trần = min(phí ship của vườn, phí sàn thu trên vườn đó).</summary>
    FreeShipping,

    /// <summary>Giảm tiền hàng, SÀN chịu (trừ vào hoa hồng). Trần = phí sàn của vườn đó.</summary>
    PlatformDiscount,

    /// <summary>Giảm tiền hàng, NGƯỜI BÁN chịu (trừ vào tiền họ nhận, không đụng hoa hồng). Trần = tiền hàng của vườn.</summary>
    SellerDiscount,

    /// <summary>
    /// [DEMO] Kéo tổng đơn về đúng <c>VoucherDiscountCalculator.DemoFlatTotalTargetVnd</c>, trừ lần lượt
    /// phí ship → hoa hồng sàn → tiền người bán. Chỉ dùng để trình bày, không phải nghiệp vụ thật.
    /// </summary>
    DemoFlatTotal,
}
