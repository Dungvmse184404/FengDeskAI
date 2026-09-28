using System.Globalization;

namespace FengDeskAI.Application.Common.Formatting;

/// <summary>
/// Định dạng tiền VND cho câu chữ gửi tới người dùng: "500.000đ". KHÔNG dùng <c>CultureInfo("vi-VN")</c> — container
/// có thể chạy chế độ globalization-invariant (không có ICU) và ném lỗi; còn format theo culture mặc định thì
/// ra "500,000" kiểu tiếng Anh.
/// </summary>
public static class Vnd
{
    private static readonly NumberFormatInfo Format = new() { NumberGroupSeparator = ".", NumberDecimalSeparator = "," };

    public static string Of(decimal amount) => amount.ToString("#,##0", Format) + "đ";
}
