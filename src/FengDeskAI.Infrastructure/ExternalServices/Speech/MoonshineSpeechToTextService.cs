using System.Net.Http.Headers;
using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Speech;

/// <summary>
/// Gọi Moonshine (microservice riêng ở moonshine-stt/, cùng hình dạng response với Whisper —
/// <c>POST /v1/audio/transcriptions</c> multipart → {"text": "..."}).
///
/// KHÁC Whisper: Moonshine train riêng 1 model / ngôn ngữ, KHÔNG tự nhận diện câu nói trộn Việt-Anh.
/// BẮT BUỘC truyền "language" ("vi"/"en") để chọn đúng model — thiếu thì mặc định "vi".
/// </summary>
public sealed class MoonshineSpeechToTextService : ISpeechToTextService
{
    private readonly HttpClient _http;
    private readonly SpeechSettings _settings;
    private readonly ILogger<MoonshineSpeechToTextService> _logger;

    public MoonshineSpeechToTextService(HttpClient http, IOptions<SpeechSettings> settings, ILogger<MoonshineSpeechToTextService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_settings.MoonshineBaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(30); // model nhỏ hơn Whisper nhiều — kỳ vọng nhanh hơn
    }

    public async Task<string> TranscribeAsync(Stream audio, string fileName, string? language = null, CancellationToken ct = default)
    {
        var model = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? _settings.MoonshineModelEn
            : _settings.MoonshineModelVi; // mặc định "vi" nếu FE không gửi/gửi giá trị lạ

        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(model), "model");
        form.Add(new StringContent("json"), "response_format");

        using var response = await _http.PostAsync("audio/transcriptions", form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[Speech] Moonshine trả {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Dịch vụ nhận diện giọng nói trả lỗi {(int)response.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("text", out var text)
            ? (text.GetString() ?? "").Trim()
            : "";
    }
}
