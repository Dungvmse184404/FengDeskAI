namespace FengDeskAI.Application.Common.Media;

/// <summary>Quy ước chung cho upload ảnh (product, chat, avatar...).</summary>
public static class ImageUpload
{
    // Chỉ các định dạng AI đọc được: JPG, PNG, BMP, GIF.
    public static readonly string[] AllowedContentTypes =
        { "image/jpeg", "image/png", "image/bmp", "image/x-ms-bmp", "image/gif" };

    public static bool IsAllowed(string? contentType)
        => contentType != null && AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static string ExtensionFor(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/bmp" or "image/x-ms-bmp" => ".bmp",
        "image/gif" => ".gif",
        _ => ".bin",
    };
}
