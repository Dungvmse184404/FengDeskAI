using System.Linq;
using System.Text.Json;
using FengDeskAI.Application.Common.Ai;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Bóc JSON khỏi phản hồi LLM. Ca quan trọng nhất là <c>INTAKE-JSON-03</c>: nó tái hiện đúng lỗi
/// "bật thinking thì intake trả draft rỗng" — model viết một bản nháp JSON toàn <c>null</c> giữa lúc
/// suy luận rồi mới viết câu trả lời thật ở cuối, mà hàm cũ lại lấy object ĐẦU TIÊN.
/// </summary>
public sealed class AiJsonExtractorTests
{
    /// <summary>Ứng viên "có nội dung" = parse được và có ít nhất một field khác null.</summary>
    private static bool HasAnyValue(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().Any(p =>
            p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            && !(p.Value.ValueKind is JsonValueKind.Array && p.Value.GetArrayLength() == 0));
    }

    private static string? Best(string text) => AiJsonExtractor.SelectBestJsonObject(text, HasAnyValue);

    [Fact(DisplayName = "INTAKE-JSON-01 [Normal] Plain JSON content is returned as-is")]
    public void PlainJson_IsReturned()
    {
        const string content = """{"name":"Bàn làm việc","lighting":"Natural"}""";
        Assert.Equal(content, Best(content));
    }

    [Fact(DisplayName = "INTAKE-JSON-02 [Normal] A markdown code fence is stripped")]
    public void CodeFence_IsStripped()
    {
        const string content = "```json\n{\"name\":\"Góc đọc sách\"}\n```";
        Assert.Equal("{\"name\":\"Góc đọc sách\"}", Best(content));
    }

    [Fact(DisplayName = "INTAKE-JSON-03 [Abnormal] A null draft inside the thinking block is skipped")]
    public void ThinkingDraft_IsSkipped_AndTheRealAnswerWins()
    {
        // Dạng thật khi bật thinking: model chép schema ra nháp (toàn null) rồi mới chốt ở cuối.
        const string content = """
            Người dùng nói "bàn gỗ cạnh cửa sổ, nhiều nắng". Để tôi dựng khung đã:
            {"name":null,"locationType":null,"lighting":null,"hasDesk":null,"inputs":[]}
            Giờ điền: có nhắc bàn gỗ nên hasDesk = true, Material = Wood. "Nhiều nắng" => Natural.
            Không nói hướng nên để null.
            {"name":"Bàn làm việc cạnh cửa sổ","locationType":"Home","lighting":"Natural","hasDesk":true,
             "inputs":[{"kind":"Material","code":"Wood"}],"mentionedFields":["lighting","hasDesk","inputs"]}
            """;

        var best = Best(content);

        Assert.NotNull(best);
        Assert.Contains("Bàn làm việc cạnh cửa sổ", best);
        Assert.Contains("Natural", best);
        // Bản nháp rỗng KHÔNG được chọn — đây chính là thứ từng lọt ra FE.
        Assert.DoesNotContain("\"name\":null", best);
    }

    [Fact(DisplayName = "INTAKE-JSON-04 [Boundary] An all-null answer is still returned when nothing else exists")]
    public void AllNullAnswer_IsStillReturned()
    {
        // "Phòng tôi khá đẹp" → mọi field null là câu trả lời ĐÚNG, không được coi là hỏng.
        const string content = """Không đủ thông tin. {"name":null,"lighting":null,"inputs":[]}""";

        var best = Best(content);

        Assert.NotNull(best);
        Assert.Contains("\"name\":null", best);
    }

    [Fact(DisplayName = "INTAKE-JSON-05 [Abnormal] Braces inside strings do not break brace counting")]
    public void BracesInsideStrings_AreIgnored()
    {
        const string content = """{"name":"Phòng {đặc biệt} của tôi","lighting":"Natural"}""";

        var best = Best(content);

        Assert.Equal(content, best);
        using var doc = JsonDocument.Parse(best!);
        Assert.Equal("Phòng {đặc biệt} của tôi", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact(DisplayName = "INTAKE-JSON-06 [Abnormal] A truncated object is skipped in favour of a complete one")]
    public void TruncatedObject_IsSkipped()
    {
        const string content = """
            Nháp bị cắt: {"name":"Chưa xong","lighting":
            Viết lại: {"name":"Góc làm việc","lighting":"Natural"}
            """;

        var best = Best(content);

        Assert.NotNull(best);
        Assert.Contains("Góc làm việc", best);
    }

    [Fact(DisplayName = "INTAKE-JSON-07 [Abnormal] Text with no JSON object returns null")]
    public void NoJson_ReturnsNull()
    {
        Assert.Null(Best("Tôi không chắc về không gian này."));
    }

    [Fact(DisplayName = "INTAKE-JSON-08 [Normal] Nested objects are not reported as separate candidates")]
    public void NestedObjects_AreNotSeparateCandidates()
    {
        const string content = """{"a":1,"inputs":[{"kind":"Color","code":"Green"}]}""";

        var all = AiJsonExtractor.ExtractJsonObjects(content);

        Assert.Single(all);
        Assert.Equal(content, all[0]);
    }
}
