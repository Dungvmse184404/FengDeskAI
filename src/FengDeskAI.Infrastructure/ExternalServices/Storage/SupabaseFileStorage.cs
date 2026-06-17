using System.Net;
using System.Net.Http.Headers;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Storage;

/// <summary>
/// Lưu file lên Supabase Storage qua REST API (<c>/storage/v1/object</c>).
/// Bucket phải để Public để URL công khai đọc được; ghi yêu cầu service_role key.
/// </summary>
public sealed class SupabaseFileStorage : IFileStorage
{
    private readonly HttpClient _http;
    private readonly SupabaseStorageOptions _opts;
    private readonly ILogger<SupabaseFileStorage> _logger;

    public SupabaseFileStorage(HttpClient http, IOptions<SupabaseStorageOptions> options, ILogger<SupabaseFileStorage> logger)
    {
        _opts = options.Value;
        _logger = logger;
        _http = http;

        if (!string.IsNullOrWhiteSpace(_opts.Url))
            _http.BaseAddress = new Uri(_opts.Url.TrimEnd('/') + "/");

        if (!string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("apikey", _opts.ApiKey);
        }
    }

    public async Task<StoredFile> UploadAsync(string objectPath, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = $"storage/v1/object/{_opts.Bucket}/{objectPath}";
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.TryAddWithoutValidation("x-upsert", "true");

        var body = new StreamContent(content);
        body.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        req.Content = body;

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("[Storage] Upload thất bại {Status}: {Detail}", resp.StatusCode, detail);
            resp.EnsureSuccessStatusCode();
        }

        return new StoredFile(objectPath, GetPublicUrl(objectPath));
    }

    public async Task DeleteByUrlAsync(string publicUrl, CancellationToken ct = default)
    {
        if (!TryExtractObjectPath(publicUrl, out var objectPath))
            return;

        var path = $"storage/v1/object/{_opts.Bucket}/{objectPath}";
        using var resp = await _http.DeleteAsync(path, ct);

        // 404 = đã không còn → coi như xoá xong; lỗi khác chỉ log, không chặn nghiệp vụ.
        if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.NotFound)
            _logger.LogWarning("[Storage] Xoá object {Path} trả {Status}", objectPath, resp.StatusCode);
    }

    public string GetPublicUrl(string objectPath)
        => $"{_opts.Url.TrimEnd('/')}/storage/v1/object/public/{_opts.Bucket}/{objectPath}";

    /// <summary>Tách object path từ URL công khai ("…/{bucket}/{objectPath}").</summary>
    private bool TryExtractObjectPath(string url, out string objectPath)
    {
        objectPath = string.Empty;
        if (string.IsNullOrWhiteSpace(url)) return false;

        var marker = $"/{_opts.Bucket}/";
        var idx = url.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;

        objectPath = url[(idx + marker.Length)..];
        return objectPath.Length > 0;
    }
}
