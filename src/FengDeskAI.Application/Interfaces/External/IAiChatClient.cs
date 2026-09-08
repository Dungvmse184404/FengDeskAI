namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Vai trò của một tin nhắn trong hội thoại — khớp closed-set của LLM (Ollama/OpenAI).</summary>
public static class AiChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
    public const string Tool = "tool";
}

/// <summary>
/// Một tin nhắn hội thoại trung lập với provider. <paramref name="Images"/> là base64 thuần (Ollama "images").
/// <paramref name="ToolCalls"/>: khi role=assistant echo lại lời gọi tool. <paramref name="ToolName"/>: khi role=tool, tên tool đã chạy.
/// </summary>
public sealed record AiChatMessage(
    string Role,
    string Content,
    IReadOnlyList<string>? Images = null,
    IReadOnlyList<AiToolCall>? ToolCalls = null,
    string? ToolName = null);

/// <summary>Kết quả 1 lượt hoàn thành từ LLM. <see cref="ToolCalls"/> khác rỗng → cần chạy tool rồi gọi lại.</summary>
public sealed record AiChatCompletion(string Content, string Model, IReadOnlyList<AiToolCall>? ToolCalls = null);

/// <summary>
/// Tinh chỉnh một lượt gọi cụ thể — dùng cho tác vụ trích xuất có cấu trúc (vd workspace intake) cần
/// output ổn định/deterministic hơn hội thoại tự do. Bỏ trống → theo mặc định của model/provider.
/// </summary>
/// <param name="Think">Bật/tắt thinking của model (Ollama "think"). null = theo mặc định model.
/// Tắt (false) giúp model nhỏ đỡ "lạc" câu trả lời vào thinking block.</param>
/// <param name="Stream">Ollama "stream":true — đọc phản hồi theo từng chunk (NDJSON) thay vì đợi
/// 1 lần rồi gộp lại, KHÔNG đổi contract phía trên (vẫn trả về 1 <see cref="AiChatCompletion"/> đầy
/// đủ). Mục đích duy nhất: giữ traffic chảy liên tục qua proxy/tunnel (vd ngrok free) có ngắt kết nối
/// khi im lặng quá lâu — câu trả lời càng dài (ảnh, tool nhiều bước...) càng dễ dính nếu stream=false.</param>
/// <param name="MaxOutputTokens">
/// Trần số token model được sinh (Ollama <c>num_predict</c> / OpenAI <c>max_tokens</c>).
/// null = không giới hạn (mặc định của Ollama) — RẤT nguy hiểm khi bật think: model có thể suy luận
/// hàng nghìn token trước khi trả JSON. Với tác vụ trích xuất schema cố định, đặt trần là cách
/// rẻ nhất để chặn trường hợp xấu nhất.
/// </param>
/// <param name="NumCtx">
/// Ghi đè cửa sổ ngữ cảnh cho riêng lượt gọi này (Ollama <c>num_ctx</c>). null = theo cấu hình provider.
/// Request chỉ có chữ không cần ctx lớn như request có ảnh — ctx nhỏ hơn thì prefill nhanh hơn và
/// KV cache chiếm ít VRAM hơn.
/// </param>
public sealed record AiCompletionOptions(
    double? Temperature = null, bool JsonMode = false, bool? Think = null, bool Stream = false,
    int? MaxOutputTokens = null, int? NumCtx = null);

/// <summary>Loại delta khi stream: chuỗi suy luận (thinking) hay nội dung đáp án (content).</summary>
public enum AiStreamKind { Thinking, Content }

/// <summary>Một mẩu delta stream từ model (chỉ phần MỚI, không tích lũy). Best-effort, không lưu DB.</summary>
public readonly record struct AiStreamChunk(AiStreamKind Kind, string Text);

/// <summary>
/// Cổng gọi LLM hội thoại (Ollama / OpenAI-compatible) — thuần transport, không giữ state.
/// Việc nhớ lịch sử + chọn model + vòng lặp tool do <c>AiChatService</c> đảm nhiệm phía Application.
/// </summary>
public interface IAiChatClient
{
    /// <param name="onDelta">
    /// Nếu khác null + provider stream được: nhận delta thinking/content realtime (để đẩy "chữ chạy" lên UI).
    /// Best-effort — provider không stream thì bỏ qua, caller vẫn nhận đủ 1 <see cref="AiChatCompletion"/>.
    /// </param>
    Task<AiChatCompletion> CompleteAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        IReadOnlyList<AiToolSpec>? tools = null,
        AiCompletionOptions? options = null,
        IProgress<AiStreamChunk>? onDelta = null,
        CancellationToken ct = default);
}
