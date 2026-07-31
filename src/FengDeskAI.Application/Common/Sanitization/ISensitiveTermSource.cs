namespace FengDeskAI.Application.Common.Sanitization;

/// <summary>
/// Nguồn "từ vựng nhạy cảm" động nạp vào <see cref="IAiTextSanitizer"/> (vd tên tool đang đăng ký).
/// Tách interface để thêm nguồn mới (tên bảng DB, tên field nội bộ…) không phải sửa sanitizer.
/// Chỉ áp cho <see cref="SanitizeMode.LiveStream"/>.
/// </summary>
public interface ISensitiveTermSource
{
    /// <summary>Các từ khóa cần che. So khớp theo whole-word, không phân biệt hoa/thường.</summary>
    IReadOnlyCollection<string> Terms { get; }
}
