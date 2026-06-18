using System.Text.RegularExpressions;

namespace FengDeskAI.Application.Features.Chat;

/// <summary>Nhận diện lời gọi trợ lý AI trong nội dung tin nhắn (vd "@AI bạn thấy sản phẩm này thế nào...").</summary>
public static partial class AiMention
{
    [GeneratedRegex(@"(^|\s)@ai\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MentionRegex();

    /// <summary>True nếu nội dung có nhắc @AI (không phân biệt hoa thường, chặn "@aith) bằng word-boundary).</summary>
    public static bool Mentions(string? content)
        => !string.IsNullOrWhiteSpace(content) && MentionRegex().IsMatch(content);
}
