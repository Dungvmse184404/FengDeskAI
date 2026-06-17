namespace FengDeskAI.Application.Common.Media;

/// <summary>Quy ước chung cho upload ảnh (product, chat, avatar...).</summary>
public static class ImageUpload
{
    public static readonly string[] AllowedContentTypes =
        { "image/jpeg", "image/png", "image/webp", "image/gif" };

    public static bool IsAllowed(string? contentType)
        => contentType != null && AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    public static string ExtensionFor(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin",
    };
}
