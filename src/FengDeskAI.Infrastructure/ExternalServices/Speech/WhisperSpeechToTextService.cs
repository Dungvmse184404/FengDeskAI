using System.Net.Http.Headers;
using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Speech;

/// <summary>
/// Gọi Whisper qua endpoint chuẩn OpenAI <c>POST /audio/transcriptions</c> (multipart).
/// KHÔNG truyền tham số "language" → model tự nhận diện, xử lý được câu nói trộn Việt–Anh.
/// </summary>
public sealed class WhisperSpeechToTextService : ISpeechToTextService
{
    private readonly HttpClient _http;
    private readonly SpeechSettings _settings;
    private readonly ILogger<WhisperSpeechToTextService> _logger;

    public WhisperSpeechToTextService(HttpClient http, IOptions<SpeechSettings> settings, ILogger<WhisperSpeechToTextService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;

        _http.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(60);
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    public async Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(_settings.Model), "model");
        // response_format=json → { "text": "..." }; KHÔNG gửi "language" để model tự nhận diện vi/en/mixed.
        form.Add(new StringContent("json"), "response_format");

        using var response = await _http.PostAsync("audio/transcriptions", form, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("[Speech] Whisper trả {Status}: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Dịch vụ nhận diện giọng nói trả lỗi {(int)response.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("text", out var text)
            ? (text.GetString() ?? "").Trim()
            : "";
    }
}
