using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <inheritdoc cref="IProductModel3DService"/>
public class ProductModel3DService : IProductModel3DService
{
    private const string GlbContentType = "model/gltf-binary";

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IFileStorage _storage;
    private readonly IModel3DGenerator _generator;
    private readonly ILogger<ProductModel3DService> _logger;

    public ProductModel3DService(
        IUnitOfWork uow, IMapper mapper, IFileStorage storage,
        IModel3DGenerator generator, ILogger<ProductModel3DService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _storage = storage;
        _generator = generator;
        _logger = logger;
    }

    public async Task<IServiceResult<ProductModel3DResponse>> GetAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);

        var model = await _uow.Products.GetModel3DAsync(productId, ct);
        if (model is null)
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DNotFound);

        return ServiceResult<ProductModel3DResponse>.Success(_mapper.Map<ProductModel3DResponse>(model));
    }

    public async Task<IServiceResult<Model3DRequestResponse>> RequestAsync(
        Guid productId, Guid userId, bool isAdmin, RequestModel3DRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return ServiceResult<Model3DRequestResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<Model3DRequestResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.ManageForbidden);

        // Chỉ 1 request "đang mở" tại 1 thời điểm cho mỗi product.
        var open = await _uow.Products.GetOpenModel3DRequestAsync(productId, ct);
        if (open is not null)
            return ServiceResult<Model3DRequestResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestOpenConflict);

        // Đã có model Succeeded trước đó → Regenerate (thủ công qua staff sàn). Ngược lại → Initial (tự động).
        var currentModel = await _uow.Products.GetModel3DAsync(productId, ct);
        var isRegenerate = currentModel is { Status: Model3DStatus.Succeeded };

        var entity = new Model3DRequest
        {
            ProductId = productId,
            RequestedBy = userId,
        };

        if (isRegenerate)
        {
            // Regenerate: KHÔNG chọn ảnh ở bước này — staff sàn tự chọn ảnh khi xử lý (mục 6/7 ADR).
            entity.RequestType = Model3DRequestType.Regenerate;
            entity.Status = Model3DRequestStatus.AwaitingStaff;
        }
        else
        {
            var (ids, _, error) = await Model3DImageResolver.ResolveAsync(
                _uow, _storage, productId, request, defaultToPrimaryIfEmpty: true, ct);
            if (error != Model3DImageResolver.ErrorCode.None)
                return ServiceResult<Model3DRequestResponse>.Failure(ApiStatusCodes.BadRequest, MapImageError(error));

            entity.RequestType = Model3DRequestType.Initial;
            entity.Status = Model3DRequestStatus.Queued;
            entity.SourceImageIds = ids;
        }

        await _uow.Products.AddModel3DRequestAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        var message = isRegenerate ? ApiStatusMessages.Product.Model3DRequestAwaitingStaff : ApiStatusMessages.Product.Model3DRequestQueued;
        return ServiceResult<Model3DRequestResponse>.Success(
            _mapper.Map<Model3DRequestResponse>(entity), message, ApiStatusCodes.Accepted);
    }

    public async Task<IServiceResult<List<Model3DRequestResponse>>> ListRequestsAsync(
        Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return ServiceResult<List<Model3DRequestResponse>>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<List<Model3DRequestResponse>>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.ManageForbidden);

        var list = await _uow.Products.ListModel3DRequestsAsync(productId, ct);
        return ServiceResult<List<Model3DRequestResponse>>.Success(_mapper.Map<List<Model3DRequestResponse>>(list));
    }

    public async Task<IServiceResult> ToggleAsync(Guid productId, Guid userId, bool isAdmin, bool isEnabled, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.ManageForbidden);

        var model = await _uow.Products.GetModel3DAsync(productId, ct);
        if (model is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DNotFound);

        model.IsEnabled = isEnabled;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Product.Model3DToggled);
    }

    public async Task<IServiceResult> DeleteAsync(Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.ManageForbidden);

        var model = await _uow.Products.GetModel3DAsync(productId, ct);
        if (model is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DNotFound);

        var modelUrl = model.ModelUrl;
        _uow.Products.RemoveModel3D(model);
        await _uow.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(modelUrl))
            await _storage.DeleteByUrlAsync(modelUrl, ct);

        return ServiceResult.Success(ApiStatusMessages.Product.Model3DDeleted);
    }

    public async Task ProcessInitialQueueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var changed = false;

        // 1) Gửi task Meshy cho các request Initial đang Queued & đến hạn (mới hoặc retry sau backoff 402).
        var due = await _uow.Products.GetDueInitialQueueAsync(now, ct);
        foreach (var req in due)
        {
            var urls = await Model3DImageResolver.GetUrlsForIdsAsync(_uow, req.ProductId, req.SourceImageIds, ct);
            if (urls.Count == 0)
            {
                req.Status = Model3DRequestStatus.Failed;
                req.InternalFailureReason = Model3DFailureReason.InvalidImage;
                changed = true;
                continue;
            }

            try
            {
                var taskId = await _generator.StartImageTo3DAsync(urls, ct);
                req.MeshyTaskId = taskId;
                req.Status = Model3DRequestStatus.Processing;
                req.InternalFailureReason = null;
                req.NextAttemptAt = null;
                changed = true;
            }
            catch (InsufficientCreditsException)
            {
                var backoff = _generator.InsufficientCreditsBackoffMinutes;
                req.InternalFailureReason = Model3DFailureReason.InsufficientCredits;
                req.NextAttemptAt = now.AddMinutes(backoff);
                changed = true;
                _logger.LogWarning("[Model3D] Request {RequestId} (Initial) hết credit Meshy — retry sau {Minutes} phút.",
                    req.Id, backoff);
            }
            catch (Exception ex)
            {
                req.Status = Model3DRequestStatus.Failed;
                req.InternalFailureReason = Model3DFailureReason.GenerationFailed;
                changed = true;
                _logger.LogError(ex, "[Model3D] Gửi job Meshy thất bại cho request {RequestId} (product {ProductId}).",
                    req.Id, req.ProductId);
            }
        }

        // 2) Poll các request Initial đang Processing.
        var staleModelUrls = new List<string>();
        var processing = await _uow.Products.GetProcessingInitialRequestsAsync(ct);
        foreach (var req in processing)
        {
            Model3DTaskResult result;
            try
            {
                result = await _generator.GetTaskAsync(req.MeshyTaskId!, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Model3D] Poll task {TaskId} thất bại — thử lại lượt sau.", req.MeshyTaskId);
                continue;
            }

            switch (result.State)
            {
                case Model3DGenerationState.Running:
                    break; // chưa có gì để ghi thêm — FE tự poll GET request để thấy vẫn đang Processing

                case Model3DGenerationState.Failed:
                    req.Status = Model3DRequestStatus.Failed;
                    req.InternalFailureReason = Model3DFailureReason.GenerationFailed;
                    changed = true;
                    break;

                case Model3DGenerationState.Succeeded:
                    var staleUrl = await FinalizeInitialSucceededAsync(req, result, ct);
                    if (!string.IsNullOrWhiteSpace(staleUrl)) staleModelUrls.Add(staleUrl);
                    changed = true;
                    break;
            }
        }

        if (changed) await _uow.SaveChangesAsync(ct);

        // Xóa GLB cũ trên storage SAU KHI đã lưu DB thành công — tránh mất dữ liệu nếu SaveChanges lỗi giữa chừng.
        foreach (var url in staleModelUrls)
            await _storage.DeleteByUrlAsync(url, ct);
    }

    /// <summary>
    /// Tải GLB từ Meshy → re-host storage → ghi vào ProductModel3D (tạo mới hoặc hồi sinh bản soft-delete).
    /// Trả về URL model cũ (nếu có, để caller xóa storage SAU KHI SaveChanges thành công).
    /// </summary>
    private async Task<string?> FinalizeInitialSucceededAsync(Model3DRequest req, Model3DTaskResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.GlbUrl))
        {
            req.Status = Model3DRequestStatus.Failed;
            req.InternalFailureReason = Model3DFailureReason.GenerationFailed;
            return null;
        }

        var existing = await _uow.Products.GetModel3DIncludingDeletedAsync(req.ProductId, ct);
        var oldModelUrl = existing?.ModelUrl;

        ProductModel3D model;
        if (existing is null)
        {
            model = new ProductModel3D { ProductId = req.ProductId };
            await _uow.Products.AddModel3DAsync(model, ct);
        }
        else
        {
            model = existing;
            model.IsDeleted = false;
        }

        var sourceUrls = await Model3DImageResolver.GetUrlsForIdsAsync(_uow, req.ProductId, req.SourceImageIds, ct);
        model.SourceImageUrl = sourceUrls.FirstOrDefault() ?? string.Empty;
        model.ThumbnailUrl = result.ThumbnailUrl;
        model.Progress = 100;
        model.IsEnabled = true;

        try
        {
            await using var src = await _generator.DownloadAsync(result.GlbUrl, ct);
            using var buffer = new MemoryStream();
            await src.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            var objectPath = $"Product_models/{req.ProductId}/{Guid.NewGuid():N}.glb";
            var stored = await _storage.UploadAsync(objectPath, buffer, GlbContentType, ct);
            model.ModelUrl = stored.Url;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Model3D] Re-host GLB sang storage thất bại cho product {ProductId} — tạm dùng URL provider.",
                req.ProductId);
            model.ModelUrl = result.GlbUrl; // fallback: URL tạm của provider
        }

        model.Status = Model3DStatus.Succeeded;
        model.ErrorMessage = null;
        req.Status = Model3DRequestStatus.Succeeded;

        return oldModelUrl;
    }

    private async Task<bool> CanManageStoreAsync(Guid storeId, Guid userId, bool isAdmin, CancellationToken ct)
        => isAdmin || await _uow.Stores.CanManageAsync(storeId, userId, ct);

    private static string MapImageError(Model3DImageResolver.ErrorCode error) => error switch
    {
        Model3DImageResolver.ErrorCode.ImageNotFound => ApiStatusMessages.Product.Model3DSourceImageNotFound,
        Model3DImageResolver.ErrorCode.ImageTypeInvalid => ApiStatusMessages.Product.ImageTypeInvalid,
        Model3DImageResolver.ErrorCode.NoPrimaryImage => ApiStatusMessages.Product.Model3DSourceImageRequired,
        Model3DImageResolver.ErrorCode.TooManyImages => ApiStatusMessages.Product.Model3DImageLimitExceeded,
        _ => ApiStatusMessages.Product.Model3DImageRequired,
    };
}
