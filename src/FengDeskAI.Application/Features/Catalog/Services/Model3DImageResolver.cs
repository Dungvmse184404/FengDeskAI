using FengDeskAI.Application.Common.Media;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Gom logic chọn/validate/upload ảnh nguồn cho model 3D — dùng chung giữa
/// <see cref="ProductModel3DService"/> (tạo request) và <see cref="Model3DRequestService"/> (staff
/// generate/retry). Nguồn ảnh = <c>SourceImageIds</c> (ảnh có sẵn) + <c>NewImages</c> (upload mới,
/// lưu thành <c>ProductImage</c> bình thường) — tổng 1–4 ảnh (giới hạn Meshy multi-image-to-3d).
/// </summary>
internal static class Model3DImageResolver
{
    public enum ErrorCode
    {
        None,
        ImageNotFound,
        ImageTypeInvalid,
        NoImages,
        TooManyImages,
        NoPrimaryImage,
    }

    public static async Task<(List<Guid> Ids, List<string> Urls, ErrorCode Error)> ResolveAsync(
        IUnitOfWork uow, IFileStorage storage, Guid productId, RequestModel3DRequest request,
        bool defaultToPrimaryIfEmpty, CancellationToken ct)
    {
        var ids = new List<Guid>();
        var urls = new List<string>();

        var existingCount = request.SourceImageIds?.Distinct().Count() ?? 0;
        var newCount = request.NewImages?.Count ?? 0;
        if (existingCount + newCount > 4) return (ids, urls, ErrorCode.TooManyImages);
        if (request.NewImages is { Count: > 0 }
            && request.NewImages.Any(file => !ImageUpload.IsAllowed(file.ContentType)))
            return (ids, urls, ErrorCode.ImageTypeInvalid);

        if (request.SourceImageIds is { Count: > 0 })
        {
            foreach (var imageId in request.SourceImageIds.Distinct())
            {
                var image = await uow.Products.GetImageAsync(productId, imageId, ct);
                if (image is null) return (ids, urls, ErrorCode.ImageNotFound);
                ids.Add(image.Id);
                urls.Add(image.Url);
            }
        }

        if (request.NewImages is { Count: > 0 })
        {
            foreach (var file in request.NewImages)
            {
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(file.ContentType);
                var objectPath = $"Product_images/{productId}/{Guid.NewGuid():N}{ext}";

                var stored = await storage.UploadAsync(objectPath, file.Content, file.ContentType, ct);
                var image = new ProductImage { ProductId = productId, Url = stored.Url, SortOrder = 0 };
                await uow.Products.AddImageAsync(image, ct);

                ids.Add(image.Id);
                urls.Add(image.Url);
            }
        }

        if (ids.Count == 0 && defaultToPrimaryIfEmpty)
        {
            var images = await uow.Products.ListImagesAsync(productId, ct);
            var primary = images.OrderBy(i => i.SortOrder).FirstOrDefault();
            if (primary is null) return (ids, urls, ErrorCode.NoPrimaryImage);
            ids.Add(primary.Id);
            urls.Add(primary.Url);
        }

        if (ids.Count == 0) return (ids, urls, ErrorCode.NoImages);
        return (ids, urls, ErrorCode.None);
    }

    /// <summary>Tra URL cho các ảnh đã lưu sẵn trên 1 request (không validate lại — dùng lúc finalize/accept).</summary>
    public static async Task<List<string>> GetUrlsForIdsAsync(
        IUnitOfWork uow, Guid productId, List<Guid> imageIds, CancellationToken ct)
    {
        var urls = new List<string>();
        foreach (var id in imageIds)
        {
            var image = await uow.Products.GetImageAsync(productId, id, ct);
            if (image is not null) urls.Add(image.Url);
        }
        return urls;
    }
}
