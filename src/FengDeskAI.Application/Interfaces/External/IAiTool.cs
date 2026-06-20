using System.Text.Json;

namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Mô tả 1 tham số của tool (JSON schema rút gọn).</summary>
public sealed record AiToolParameter(string Type, string Description, bool Required = false, IReadOnlyList<string>? Enum = null);

/// <summary>Khai báo tool gửi cho LLM (không gồm phần thực thi) — transport dùng cái này.</summary>
public sealed record AiToolSpec(string Name, string Description, IReadOnlyDictionary<string, AiToolParameter> Parameters);

/// <summary>1 yêu cầu gọi tool do LLM trả về.</summary>
public sealed record AiToolCall(string Name, string ArgumentsJson);

/// <summary>
/// Ngữ cảnh thực thi tool — scope theo user để không lộ dữ liệu người khác.
/// <paramref name="ChatboxId"/>: phòng đang hội thoại (dùng cho tool đọc thông tin đối phương theo consent).
/// </summary>
public sealed record AiToolContext(Guid UserId, string? UserRole, string? UserEmail, Guid? ChatboxId = null);

/// <summary>
/// Một công cụ AI có thể gọi (function calling). Thuần Application: gọi lại các service nghiệp vụ,
/// trả kết quả JSON (chuỗi) để feed ngược cho LLM. v1 chỉ tool ĐỌC, scope theo <see cref="AiToolContext"/>.
/// </summary>
public interface IAiTool
{
    string Name { get; }
    string Description { get; }
    IReadOnlyDictionary<string, AiToolParameter> Parameters { get; }
    Task<string> ExecuteAsync(AiToolContext context, JsonElement arguments, CancellationToken ct = default);
}

public static class AiToolExtensions
{
    public static AiToolSpec ToSpec(this IAiTool tool) => new(tool.Name, tool.Description, tool.Parameters);
}
