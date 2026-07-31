using System.Text.RegularExpressions;

namespace FengDeskAI.Application.Common.Validation;

/// <summary>
/// Chuẩn hoá + kiểm tra số điện thoại Việt Nam. Dùng cho dữ liệu gửi sang nhà vận chuyển:
/// GHN chỉ nhận <b>số di động 10 chữ số bắt đầu bằng 0</b> ở <c>from_phone</c>/<c>to_phone</c>
/// (số cố định 028…, tổng đài 1900/1800 đều bị từ chối), nên hotline store không phải lúc nào
/// cũng dùng được — khi đó phải nhập <c>StoreAddress.SenderPhone</c> riêng.
/// </summary>
public static class VietnamPhone
{
    /// <summary>Di động VN: 0 + đầu số 3/5/7/8/9 + 8 chữ số = 10 ký tự.</summary>
    private static readonly Regex MobileRegex = new(@"^0[35789]\d{8}$", RegexOptions.Compiled);

    /// <summary>Bỏ khoảng trắng/dấu chấm/gạch/ngoặc và đổi tiền tố quốc tế (+84, 84) về 0.</summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return null;

        if (digits.StartsWith("84") && digits.Length == 11) digits = "0" + digits[2..];
        else if (digits.StartsWith("840") && digits.Length == 12) digits = digits[2..];

        return digits;
    }

    /// <summary>True nếu số dùng được cho API nhà vận chuyển (di động VN 10 số sau khi chuẩn hoá).</summary>
    public static bool IsCarrierValid(string? raw)
    {
        var normalized = Normalize(raw);
        return normalized is not null && MobileRegex.IsMatch(normalized);
    }

    /// <summary>
    /// Số liên hệ hợp lệ để hiển thị (hotline): di động, hoặc tổng đài 1900/1800, hoặc số cố định
    /// 10-11 số. Rộng hơn <see cref="IsCarrierValid"/> vì hotline chỉ để khách gọi, không gửi cho GHN.
    /// </summary>
    public static bool IsContactValid(string? raw)
    {
        var normalized = Normalize(raw);
        if (normalized is null) return false;
        if (MobileRegex.IsMatch(normalized)) return true;
        if (normalized.StartsWith("1900") || normalized.StartsWith("1800"))
            return normalized.Length is >= 8 and <= 11;
        return normalized.StartsWith('0') && normalized.Length is 10 or 11;
    }
}
