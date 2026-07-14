using System.Text.RegularExpressions;

namespace FengDeskAI.Application.Common;

/// <summary>
/// Làm sạch text do model AI sinh ra TRƯỚC KHI hiển thị cho user (đáp án cuối, thinking stream…).
/// Model nội bộ vẫn nhận ID đầy đủ để gọi tool — chỉ che ở tầng hiển thị.
/// </summary>
public static class AiTextSanitizer
{
    /// <summary>
    /// GUID đầy đủ đứng "trần" trong văn bản (KHÔNG sau '/', tức không phải URL như /products/{id}, và
    /// không dính token dài hơn). Dùng để rút gọn khi hiển thị cho user.
    /// </summary>
    private static readonly Regex EntityIdRegex = new(
        @"(?<![\w/-])([0-9a-fA-F]{8})-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![\w-])",
        RegexOptions.Compiled);

    /// <summary>Mọi GUID trần → 8 ký tự đầu + "-..." (vd 1b904ba9-...). Giữ nguyên GUID trong URL /products/{id}.</summary>
    public static string CensorEntityIds(string? content)
        => string.IsNullOrEmpty(content) ? content ?? string.Empty : EntityIdRegex.Replace(content, "$1-...");
}
