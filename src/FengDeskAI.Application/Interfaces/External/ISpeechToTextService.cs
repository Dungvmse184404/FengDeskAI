namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Chuyển âm thanh → text (STT) qua engine Whisper. Hỗ trợ tiếng Việt + Anh + nói trộn (code-switching)
/// — không cần chọn ngôn ngữ trước, model tự nhận diện. Provider cấu hình qua Speech:BaseUrl
/// (Groq cloud hoặc faster-whisper self-host — cùng chuẩn OpenAI /audio/transcriptions).
/// </summary>
public interface ISpeechToTextService
{
    /// <param name="audio">Stream audio (webm/ogg/mp3/wav/m4a...).</param>
    /// <param name="fileName">Tên file kèm đuôi — provider dựa vào đuôi để nhận diện định dạng.</param>
    /// <returns>Text đã nhận diện (đã trim).</returns>
    Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct = default);
}
