namespace FengDeskAI.Application.Common.Ai;

/// <summary>
/// Bóc object JSON ra khỏi phản hồi của LLM. Tách khỏi service để test được tử tế — đây là chỗ hỏng
/// khó thấy nhất của cả luồng intake, và nó chỉ hỏng khi bật thinking.
///
/// <para>
/// <b>Vì sao không chỉ lấy object ĐẦU TIÊN.</b> Lúc bật thinking, khối suy luận đi kèm phản hồi (và khi
/// <c>content</c> rỗng thì transport đưa thẳng khối đó sang đây). Trong lúc "nghĩ", model rất hay viết ra
/// một mẩu JSON dở — chép lại schema, hoặc thử một bản nháp toàn <c>null</c> — rồi mới viết câu trả lời
/// thật ở CUỐI. Lấy object đầu tiên là lấy đúng bản nháp đó: nó parse sạch, không ném exception nào, nên
/// cơ chế "thử lại không-think" (bắt <c>JsonException</c>) KHÔNG hề chạy, và người dùng nhận một draft
/// rỗng trơn với <c>confidence = 0</c>. Không-think thì <c>content</c> là JSON thuần nên object đầu tiên
/// tình cờ đúng — đó là lý do lỗi chỉ xuất hiện khi bật thinking.
/// </para>
/// </summary>
public static class AiJsonExtractor
{
    /// <summary>Gỡ hàng rào ```/```json bao quanh nếu model trả kiểu markdown.</summary>
    public static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0) return trimmed;
        trimmed = trimmed[(firstNewline + 1)..];

        var fenceEnd = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return (fenceEnd >= 0 ? trimmed[..fenceEnd] : trimmed).Trim();
    }

    /// <summary>
    /// MỌI object JSON cân ngoặc trong text, theo thứ tự xuất hiện. Object lồng nhau không được kể
    /// riêng — chỉ các object ở mức ngoài cùng.
    /// <para>
    /// Đếm ngoặc có nhận biết chuỗi + escape: một dấu <c>{</c> nằm trong chuỗi mà tính vào độ sâu là
    /// lệch toàn bộ phần sau.
    /// </para>
    /// </summary>
    public static List<string> ExtractJsonObjects(string text)
    {
        var found = new List<string>();
        var start = text.IndexOf('{');

        while (start >= 0)
        {
            int depth = 0;
            bool inString = false, escaped = false;
            int end = -1;

            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];

                if (escaped) { escaped = false; continue; }
                if (inString && c == '\\') { escaped = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (c == '{') depth++;
                else if (c == '}' && --depth == 0) { end = i; break; }
            }

            if (end >= 0)
            {
                found.Add(text[start..(end + 1)]);
                start = text.IndexOf('{', end + 1);   // tiếp tục SAU object vừa đóng
            }
            else
            {
                // Mở mà không đóng (bị cắt giữa chừng) → thử dấu { kế tiếp.
                start = text.IndexOf('{', start + 1);
            }
        }

        return found;
    }

    /// <summary>
    /// Object "đáng tin" nhất: duyệt từ CUỐI lên và lấy cái đầu tiên mà <paramref name="isMeaningful"/>
    /// chấp nhận — câu trả lời thật nằm sau khối suy luận. Không cái nào có nội dung thì trả về cái
    /// CUỐI cùng parse được (phản hồi "mọi field null" là hợp lệ, vd mô tả không nói gì cụ thể);
    /// <c>null</c> khi text không có object nào.
    /// </summary>
    public static string? SelectBestJsonObject(string text, Func<string, bool> isMeaningful)
    {
        var candidates = ExtractJsonObjects(StripCodeFence(text));
        if (candidates.Count == 0) return null;

        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (isMeaningful(candidates[i])) return candidates[i];
        }

        return candidates[^1];
    }
}
