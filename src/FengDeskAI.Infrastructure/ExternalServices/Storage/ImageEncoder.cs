using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Infrastructure.ExternalServices.Storage;

/// <summary>Encode ảnh sang base64 để feed LLM. Tải URL bằng HttpClient riêng (không gắn key Supabase).</summary>
public sealed class ImageEncoder : IImageEncoder
{
    private readonly HttpClient _http;

    public ImageEncoder(HttpClient http) => _http = http;

    public string ToBase64(byte[] bytes) => Convert.ToBase64String(bytes);

    public string ToDataUri(byte[] bytes, string contentType)
    {
        var ct = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        return $"data:{ct};base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<string> FetchAsBase64Async(string imageUrl, CancellationToken ct = default)
    {
        var bytes = await _http.GetByteArrayAsync(imageUrl, ct);
        return Convert.ToBase64String(bytes);
    }
}
