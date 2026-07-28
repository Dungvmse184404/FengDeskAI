namespace FengDeskAI.Infrastructure.ExternalServices.Speech;

/// <summary>
/// Cấu hình engine STT (section "Speech"). BaseUrl trỏ tới bất kỳ server nào nói chuẩn OpenAI
/// <c>POST {BaseUrl}/audio/transcriptions</c>. Mặc định: self-host PhoWhisper (fine-tune tiếng Việt)
/// trên faster-whisper/speaches, ApiKey để trống. Có thể đổi sang Groq cloud (whisper-large-v3 + ApiKey)
/// hoặc Whisper đa ngữ (Systran/faster-whisper-large-v3) nếu cần nói trộn vi–en.
///
/// <see cref="Provider"/> chọn implementation ISpeechToTextService qua DI factory
/// (xem DependencyInjection.cs) — đổi qua lại giữa Whisper/Moonshine chỉ bằng config, không rebuild.
/// </summary>
public class SpeechSettings
{
    public const string SectionName = "Speech";

    /// <summary>Switch bật/tắt STT ở BE. false → endpoint transcriptions trả 503 → FE fallback Web Speech.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>"Whisper" (mặc định, tự nhận diện đa ngôn ngữ) | "Moonshine" (nhẹ hơn, cần chọn ngôn ngữ).</summary>
    public string Provider { get; set; } = "Whisper";

    // --- Whisper (BaseUrl chuẩn OpenAI /audio/transcriptions, xem docker-compose.whisper.yml) ---
    public string BaseUrl { get; set; } = "http://localhost:8000/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "kiendt/PhoWhisper-large-ct2";

    // --- Moonshine (microservice riêng ở moonshine-stt/, xem docker-compose.moonshine.yml) ---
    public string MoonshineBaseUrl { get; set; } = "http://localhost:8001/v1";
    public string MoonshineModelVi { get; set; } = "moonshine/tiny-vi";
    public string MoonshineModelEn { get; set; } = "moonshine/tiny";

    /// <summary>Giới hạn kích thước audio nhận từ client (MB) — chống upload quá khổ.</summary>
    public int MaxFileSizeMb { get; set; } = 15;
}
