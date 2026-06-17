namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Chuyển ảnh sang base64 để feed cho LLM đa phương thức (Ollama field "images" nhận base64 thuần).
/// Ảnh trong chat KHÔNG lưu nhị phân — chỉ lưu link; khi cần đưa cho AI thì tải link rồi encode.
/// </summary>
public interface IImageEncoder
{
    /// <summary>Base64 thuần (không prefix) — dùng cho field "images" của Ollama.</summary>
    string ToBase64(byte[] bytes);

    /// <summary>Data URI "data:{contentType};base64,..." — dùng cho client/web hoặc provider yêu cầu.</summary>
    string ToDataUri(byte[] bytes, string contentType);

    /// <summary>Tải ảnh từ URL (vd link Supabase đã lưu) rồi trả base64 thuần.</summary>
    Task<string> FetchAsBase64Async(string imageUrl, CancellationToken ct = default);
}
