namespace FengDeskAI.Infrastructure.ExternalServices.Speech;

/// <summary>
/// Cấu hình engine STT (section "Speech"). BaseUrl trỏ tới bất kỳ server nào nói chuẩn OpenAI
/// <c>POST {BaseUrl}/audio/transcriptions</c>. Mặc định: self-host PhoWhisper (fine-tune tiếng Việt)
/// trên faster-whisper/speaches, ApiKey để trống. Có thể đổi sang Groq cloud (whisper-large-v3 + ApiKey)
/// hoặc Whisper đa ngữ (Systran/faster-whisper-large-v3) nếu cần nói trộn vi–en.
/// </summary>
public class SpeechSettings
{
    public const string SectionName = "Speech";

    /// <summary>Switch bật/tắt STT ở BE. false → endpoint transcriptions trả 503 → FE fallback Web Speech.</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:8000/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "kiendt/PhoWhisper-large-ct2";

    /// <summary>Giới hạn kích thước audio nhận từ client (MB) — chống upload quá khổ.</summary>
    public int MaxFileSizeMb { get; set; } = 15;
}
