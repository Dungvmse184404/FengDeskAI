using System.Text.RegularExpressions;

namespace FengDeskAI.Application.Common.Sanitization;

/// <summary>
/// Impl mặc định của <see cref="IAiTextSanitizer"/>. Thuần regex, stateless → đăng ký Singleton.
/// Model nội bộ vẫn nhận ID/dữ liệu đầy đủ để gọi tool — chỉ che ở tầng hiển thị.
/// </summary>
public sealed class AiTextSanitizer : IAiTextSanitizer
{
    private const string GuidBody =
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

    /// <summary>
    /// GUID đầy đủ đứng "trần" (KHÔNG sau '/', tức không phải URL như /products/{id}, và không dính token
    /// dài hơn) → rút gọn còn 8 ký tự đầu. Dùng cho đáp án cuối, nơi link sản phẩm phải còn nguyên.
    /// </summary>
    private static readonly Regex BareGuid = new(
        $@"(?<![\w/-])([0-9a-fA-F]{{8}})-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{4}}-[0-9a-fA-F]{{12}}(?![\w-])",
        RegexOptions.Compiled);

    /// <summary>Mọi GUID đầy đủ, KỂ CẢ nằm trong URL — chỉ dùng cho kênh ephemeral.</summary>
    private static readonly Regex AnyGuid = new(GuidBody, RegexOptions.Compiled);

    /// <summary>
    /// Mảnh hex còn sót sau khi chuỗi bị cắt (thinking stream chỉ gửi ~180 ký tự cuối → GUID hay bị
    /// đứt đầu, khi đó <see cref="AnyGuid"/> không nhận ra và ID rò ra nguyên si).
    /// Bắt: (a) ≥3 nhóm hex nối bằng '-' VÀ tổng ≥14 ký tự — ngưỡng này chặn ngày "2024-01-15" (10 ký tự,
    /// toàn chữ số hex hợp lệ) lọt vào; (b) dải hex liền ≥12 ký tự CÓ ít nhất 1 chữ a-f — chặn số tiền
    /// / timestamp toàn chữ số.
    /// </summary>
    private static readonly Regex HexFragment = new(
        @"(?<![0-9a-zA-Z])(?:(?=[0-9a-fA-F-]{14,})(?:[0-9a-fA-F]+-){2,}[0-9a-fA-F]+|(?=[0-9a-fA-F]*[a-fA-F])[0-9a-fA-F]{12,})(?![0-9a-zA-Z])",
        RegexOptions.Compiled);

    /// <summary>
    /// GUID bị cắt ở MÉP CUỐI của tail (đuôi đang lớn dần, vd "…/products/7f3c21ab-9d44") — chưa đủ dài
    /// để <see cref="HexFragment"/> nhận ra. Chỉ áp ở cuối chuỗi nên không đụng text bình thường.
    /// Còn sót tối đa 8 ký tự hex đầu — ngang mức <see cref="SanitizeMode.UserMessage"/> vẫn để lộ.
    /// </summary>
    private static readonly Regex TrailingPartialId = new(
        @"(?<![0-9a-zA-Z])[0-9a-fA-F]{8}-[0-9a-fA-F-]*$", RegexOptions.Compiled);

    private static readonly Regex Email = new(
        @"[\w.+-]+@[\w-]+\.[\w.-]{2,}", RegexOptions.Compiled);

    /// <summary>SĐT VN. Chỉ nhận "+84…" hoặc "0…" — "84…" trần dễ trùng số tiền (8.400.000.000đ).</summary>
    private static readonly Regex PhoneVn = new(
        @"(?<![\d.,])(?:\+84|0)\d{8,10}(?!\d)", RegexOptions.Compiled);

    /// <summary>URL tuyệt đối — che luôn checkoutUrl/QR PayOS model lỡ "nghĩ" ra.</summary>
    private static readonly Regex AbsoluteUrl = new(
        @"https?://\S+", RegexOptions.Compiled);

    private readonly Lazy<Regex?> _terms;

    public AiTextSanitizer(IEnumerable<ISensitiveTermSource> termSources)
    {
        // Lazy: một số nguồn (vd tên tool) phải mở scope DI để đọc → chỉ trả giá 1 lần, ở lần lọc đầu.
        _terms = new Lazy<Regex?>(() => BuildTermRegex(termSources), isThreadSafe: true);
    }

    public string Sanitize(string? text, SanitizeMode mode)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        if (mode == SanitizeMode.UserMessage)
            return BareGuid.Replace(text, "$1-...");

        // LiveStream: thứ tự có ý nghĩa — URL trước (nuốt cả GUID trong path), rồi ID, rồi PII.
        var s = AbsoluteUrl.Replace(text, "[link]");
        s = AnyGuid.Replace(s, "[id]");
        s = HexFragment.Replace(s, "[id]");
        s = TrailingPartialId.Replace(s, "[id]");
        s = Email.Replace(s, "[email]");
        s = PhoneVn.Replace(s, "[phone]");
        if (_terms.Value is { } terms) s = terms.Replace(s, "[internal]");
        return s;
    }

    /// <summary>Gộp mọi từ khóa nhạy cảm thành 1 regex alternation (whole-word, ignore-case).</summary>
    private static Regex? BuildTermRegex(IEnumerable<ISensitiveTermSource> sources)
    {
        var words = sources
            .SelectMany(s =>
            {
                // Nguồn lỗi (vd DI chưa sẵn sàng) không được làm chết cả bộ lọc.
                try { return s.Terms; }
                catch { return Array.Empty<string>(); }
            })
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(t => t.Length) // từ dài khớp trước, tránh bị từ ngắn "ăn" mất
            .Select(Regex.Escape)
            .ToList();

        return words.Count == 0
            ? null
            : new Regex($@"(?<![\w-])(?:{string.Join('|', words)})(?![\w-])",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
