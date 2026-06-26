using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Media;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.Storage.Services;

/// <summary>
/// Upload ảnh dùng chung (không gắn vào entity nào) — trả về URL công khai để FE đính kèm sau
/// (vd ảnh sản phẩm lúc tạo, khi chưa có productId). Lưu vào storage thư mục "uploads/".
/// </summary>
public interface IUploadService
{
    Task<IServiceResult<string>> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
}

public class UploadService : IUploadService
{
    private readonly IFileStorage _storage;

    public UploadService(IFileStorage storage) => _storage = storage;

    public async Task<IServiceResult<string>> UploadImageAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (content is null || content.Length == 0)
            return ServiceResult<string>.Failure(ApiStatusCodes.BadRequest, "Thiếu tệp ảnh.");
        if (!ImageUpload.IsAllowed(contentType))
            return ServiceResult<string>.Failure(ApiStatusCodes.UnprocessableEntity, "Định dạng ảnh không hợp lệ (chỉ JPG/PNG/BMP/GIF).");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(contentType);
        var objectPath = $"uploads/{Guid.NewGuid():N}{ext}";

        var stored = await _storage.UploadAsync(objectPath, content, contentType, ct);
        return ServiceResult<string>.Success(stored.Url, "Tải ảnh lên thành công.", ApiStatusCodes.Created);
    }
}
