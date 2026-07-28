namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Chuyển âm thanh → text (STT). Provider chọn qua Speech:Provider ("Whisper" mặc định | "Moonshine"):
/// - Whisper: 1 model đa ngôn ngữ, tự nhận diện Việt/Anh/nói trộn — không cần tham số <paramref name="language"/>.
/// - Moonshine: mỗi ngôn ngữ 1 model riêng (nhẹ hơn, nhanh hơn) — BẮT BUỘC <paramref name="language"/>
///   để chọn đúng model, KHÔNG tự nhận diện được câu nói trộn ngôn ngữ.
/// </summary>
public interface ISpeechToTextService
{
    /// <param name="audio">Stream audio (webm/ogg/mp3/wav/m4a...).</param>
    /// <param name="fileName">Tên file kèm đuôi — provider dựa vào đuôi để nhận diện định dạng.</param>
    /// <param name="language">
    /// "vi" | "en" — provider không cần ngôn ngữ (Whisper) thì bỏ qua tham số này; provider cần
    /// (Moonshine) mà thiếu thì mặc định "vi".
    /// </param>
    /// <returns>Text đã nhận diện (đã trim).</returns>
    Task<string> TranscribeAsync(Stream audio, string fileName, string? language = null, CancellationToken ct = default);
}
