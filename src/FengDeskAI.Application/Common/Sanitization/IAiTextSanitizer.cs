namespace FengDeskAI.Application.Common.Sanitization;

/// <summary>
/// Bộ lọc dùng chung cho MỌI text do model AI sinh ra trước khi tới user (đáp án cuối, thinking stream,
/// narration, nhãn hoạt động…). Mọi kênh hiển thị mới PHẢI đi qua đây thay vì tự viết regex riêng.
/// </summary>
public interface IAiTextSanitizer
{
    /// <summary>Trả text đã lọc theo <paramref name="mode"/>. null/rỗng → chuỗi rỗng. Idempotent.</summary>
    string Sanitize(string? text, SanitizeMode mode);
}
