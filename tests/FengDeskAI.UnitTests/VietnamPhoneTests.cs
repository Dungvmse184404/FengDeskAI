using FengDeskAI.Application.Common.Validation;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Chuẩn hoá và kiểm tra số điện thoại — chốt chặn giữa dữ liệu khách nhập và API nhà vận chuyển.
///
/// Vì sao đáng test kỹ: GHN chỉ nhận **di động 10 số bắt đầu bằng 0** ở <c>from_phone</c>/
/// <c>to_phone</c>. Lọt một số cố định hay tổng đài xuống tới đó thì đơn đã thu tiền rồi mới hỏng ở
/// khâu tạo vận đơn — sửa lúc đó tốn hơn nhiều so với chặn ở đây. Đồng thời hai hàm kiểm tra có
/// **độ rộng khác nhau có chủ ý** (hotline hiển thị rộng hơn số gửi cho nhà vận chuyển), nên phần
/// cần khẳng định là ranh giới giữa chúng, không chỉ là "số hợp lệ thì true".
/// </summary>
public class VietnamPhoneTests
{
    // ---------------- Chuẩn hoá ----------------

    [Theory(DisplayName = "PHONE-01 [Normal] Separators are stripped during normalisation")]
    [InlineData("0901234567", "0901234567")]
    [InlineData("090 123 4567", "0901234567")]
    [InlineData("090-123-4567", "0901234567")]
    [InlineData("(090) 123.4567", "0901234567")]
    public void Normalize_StripsSeparators(string raw, string expected)
        => Assert.Equal(expected, VietnamPhone.Normalize(raw));

    [Theory(DisplayName = "PHONE-02 [Normal] The international prefix is converted back to a leading zero")]
    [InlineData("+84901234567", "0901234567")]
    [InlineData("84901234567", "0901234567")]
    [InlineData("+84 90 123 4567", "0901234567")]
    public void Normalize_ConvertsInternationalPrefix(string raw, string expected)
        => Assert.Equal(expected, VietnamPhone.Normalize(raw));

    [Theory(DisplayName = "PHONE-03 [Abnormal] Input with no digits normalises to null")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gọi cho tôi nhé")]
    public void Normalize_WithoutDigits_ReturnsNull(string? raw)
        => Assert.Null(VietnamPhone.Normalize(raw));

    // ---------------- Số gửi cho nhà vận chuyển ----------------

    [Theory(DisplayName = "PHONE-04 [Normal] A Vietnamese mobile number is accepted by the carrier check")]
    [InlineData("0901234567")]
    [InlineData("0387654321")]
    [InlineData("0512345678")]
    [InlineData("0777777777")]
    [InlineData("0999999999")]
    [InlineData("+84901234567")]
    public void IsCarrierValid_Mobile_ReturnsTrue(string raw)
        => Assert.True(VietnamPhone.IsCarrierValid(raw));

    [Theory(DisplayName = "PHONE-05 [Abnormal] Landlines and hotlines are refused by the carrier check")]
    [InlineData("02812345678")] // cố định TP.HCM
    [InlineData("1900123456")]  // tổng đài
    [InlineData("18001234")]    // tổng đài miễn phí
    [InlineData("0401234567")]  // đầu số 4 không phải di động
    [InlineData("0601234567")]  // đầu số 6 không phải di động
    [InlineData("0901234")]     // quá ngắn
    [InlineData("09012345678")] // quá dài
    [InlineData("1901234567")]  // không bắt đầu bằng 0
    [InlineData(null)]
    public void IsCarrierValid_NonMobile_ReturnsFalse(string? raw)
        => Assert.False(VietnamPhone.IsCarrierValid(raw));

    // ---------------- Số liên hệ hiển thị ----------------

    [Theory(DisplayName = "PHONE-06 [Normal] The contact check also accepts landlines and hotlines")]
    [InlineData("0901234567")]  // di động
    [InlineData("02812345678")] // cố định 11 số
    [InlineData("0281234567")]  // cố định 10 số
    [InlineData("1900123456")]  // tổng đài
    [InlineData("18001234")]    // tổng đài miễn phí
    public void IsContactValid_ContactNumbers_ReturnTrue(string raw)
        => Assert.True(VietnamPhone.IsContactValid(raw));

    [Theory(DisplayName = "PHONE-07 [Abnormal] The contact check still refuses malformed numbers")]
    [InlineData("123")]
    [InlineData("0")]
    [InlineData("999999999999999")]
    [InlineData("abc")]
    [InlineData(null)]
    public void IsContactValid_Malformed_ReturnsFalse(string? raw)
        => Assert.False(VietnamPhone.IsContactValid(raw));

    [Fact(DisplayName = "PHONE-08 [Boundary] Every carrier-valid number is also contact-valid, but not the reverse")]
    public void CarrierValid_IsStricterThanContactValid()
    {
        // Ranh giới giữa hai hàm là thứ dễ đảo ngược nhất khi ai đó sửa regex: nếu số cố định lọt
        // qua IsCarrierValid thì đơn hàng sẽ hỏng ở khâu tạo vận đơn, sau khi đã thu tiền.
        const string mobile = "0901234567";
        const string landline = "02812345678";

        Assert.True(VietnamPhone.IsCarrierValid(mobile));
        Assert.True(VietnamPhone.IsContactValid(mobile));

        Assert.False(VietnamPhone.IsCarrierValid(landline));
        Assert.True(VietnamPhone.IsContactValid(landline));
    }
}
