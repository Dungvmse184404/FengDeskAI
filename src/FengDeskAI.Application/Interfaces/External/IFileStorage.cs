namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Kết quả 1 lần upload: đường dẫn object trong bucket + URL công khai để lưu vào DB.</summary>
public sealed record StoredFile(string ObjectPath, string Url);

/// <summary>
/// Lưu trữ file (ảnh) trên object storage — impl mặc định là Supabase Storage (bucket "Fengdesk_bucket").
/// Caller tự dựng <paramref name="objectPath"/> theo quy ước thư mục, vd "Product_images/{productId}/{guid}.jpg".
/// </summary>
public interface IFileStorage
{
    /// <summary>Tải file lên <paramref name="objectPath"/> (ghi đè nếu trùng). Trả URL công khai.</summary>
    Task<StoredFile> UploadAsync(string objectPath, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Xoá object dựa trên URL công khai đã lưu (best-effort). Bỏ qua nếu URL không thuộc bucket.</summary>
    Task DeleteByUrlAsync(string publicUrl, CancellationToken ct = default);

    /// <summary>URL công khai của một object (không gọi mạng).</summary>
    string GetPublicUrl(string objectPath);
}
