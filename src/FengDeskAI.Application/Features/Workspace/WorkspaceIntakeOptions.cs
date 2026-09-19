namespace FengDeskAI.Application.Features.Workspace;

/// <summary>
/// Cấu hình AI cho workspace intake / autofill (bind từ section "Ai:Intake").
/// Tác vụ trích xuất JSON có cấu trúc → cần output deterministic: temperature thấp + JSON mode.
/// </summary>
public sealed class WorkspaceIntakeOptions
{
    public const string SectionName = "Ai:Intake";

    /// <summary>Model dùng cho intake khi CHỈ có mô tả chữ (không ảnh) — có thể là model text nhanh.</summary>
    public string Model { get; set; } = "SimonPu/Qwen3-Coder:30B-Instruct_Q4_K_XL";

    /// <summary>
    /// Model dùng khi request CÓ đính kèm ảnh — BẮT BUỘC là model vision (vd qwen3-vl). Model text thuần
    /// (qwen3.5) sẽ bỏ qua ảnh → không nhận ra màu/cây cảnh/vật trang trí trong hình. Bỏ trống → dùng <see cref="Model"/>.
    /// </summary>
    public string? VisionModel { get; set; } = "qwen3-vl:lastest";

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
    /// Trần token model được sinh khi <b>KHÔNG</b> bật think (Ollama <c>num_predict</c>).
    /// JSON theo schema intake chỉ tốn ~250–350 token; để dư gấp đôi cho an toàn.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 700;

    /// <summary>
    /// Trần token khi CÓ bật think — khối suy luận TÍNH VÀO trần này, nên phải rộng hơn NHIỀU.
    /// <para>
    /// CẢNH BÁO khi chỉnh xuống: hết trần giữa lúc đang suy luận thì model KHÔNG kịp viết JSON,
    /// Ollama trả content rỗng (chỉ có thinking) → parse thất bại. Trần phải đủ cho
    /// cả khối suy luận LẪN ~300 token JSON ở cuối. 1200 là quá chặt, đã gây lỗi này.
    /// </para>
    /// </summary>
    public int MaxOutputTokensWhenThinking { get; set; } = 3000;

    /// <summary>
    /// Cửa sổ ngữ cảnh cho request CHỈ CÓ CHỮ. Prompt intake ~2.4k token + output ≤1.2k → 8192 là dư.
    /// Nhỏ hơn cấu hình chung (16384) → prefill nhanh hơn, KV cache chiếm ít VRAM hơn.
    /// </summary>
    public int NumCtxText { get; set; } = 8192;

    /// <summary>
    /// Cửa sổ ngữ cảnh cho request CÓ ẢNH — ảnh tiêu tốn rất nhiều token nên phải rộng.
    /// 0 = theo cấu hình provider (Ai:Relay).
    /// </summary>
    public int NumCtxVision { get; set; } = 16384;

    /// <summary>
    /// Ollama "stream":true — đọc phản hồi theo từng chunk thay vì đợi 1 lần (client vẫn nhận JSON đầy
    /// đủ như cũ, chỉ đổi cách đọc wire). Giữ kết nối "sống" qua ngrok/proxy khi mô tả kèm ảnh khiến
    /// model sinh lâu — stream=false từng khiến tunnel free-tier ngắt do im lặng quá lâu.
    /// </summary>
    public bool Stream { get; set; } = true;
}
 