namespace FengDeskAI.Application.Features.Workspace;

/// <summary>
/// Cấu hình AI cho workspace intake / autofill (bind từ section "Ai:Intake").
/// Tác vụ trích xuất JSON có cấu trúc → cần output deterministic: temperature thấp + JSON mode.
/// </summary>
public sealed class WorkspaceIntakeOptions
{
    public const string SectionName = "Ai:Intake";

    /// <summary>Model dùng cho intake khi CHỈ có mô tả chữ (không ảnh) — có thể là model text nhanh.</summary>
    public string Model { get; set; } = "gemma3:4b";

    /// <summary>
    /// Model dùng khi request CÓ đính kèm ảnh — BẮT BUỘC là model vision (vd qwen3-vl). Model text thuần
    /// (qwen3.5) sẽ bỏ qua ảnh → không nhận ra màu/cây cảnh/vật trang trí trong hình. Bỏ trống → dùng <see cref="Model"/>.
    /// </summary>
    public string? VisionModel { get; set; } = "qwen3-vl:8b";

    /// <summary>Temperature — mặc định 0 để output ổn định/deterministic. null = theo mặc định model.</summary>
    public double? Temperature { get; set; } = 0;

    /// <summary>Ép model trả JSON hợp lệ (Ollama format="json").</summary>
    public bool JsonMode { get; set; } = true;

    /// <summary>
    /// Bật/tắt thinking của model. null = theo mặc định model. Intake để FALSE — tác vụ trích xuất JSON
    /// không cần chain-of-thought; bật think khiến model sinh khối suy luận dài → chậm gấp nhiều lần.
    /// </summary>
    public bool? Think { get; set; } = false;

    /// <summary>
    /// Ollama "stream":true — đọc phản hồi theo từng chunk thay vì đợi 1 lần (client vẫn nhận JSON đầy
    /// đủ như cũ, chỉ đổi cách đọc wire). Giữ kết nối "sống" qua ngrok/proxy khi mô tả kèm ảnh khiến
    /// model sinh lâu — stream=false từng khiến tunnel free-tier ngắt do im lặng quá lâu.
    /// </summary>
    public bool Stream { get; set; } = true;
}
 