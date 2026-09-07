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

/// <inheritdoc cref="IModel3DRequestService"/>
public class Model3DRequestService : IModel3DRequestService
{
    private const string GlbContentType = "model/gltf-binary";

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IFileStorage _storage;
    private readonly IModel3DGenerator _generator;
    private readonly ILogger<Model3DRequestService> _logger;

    public Model3DRequestService(
        IUnitOfWork uow, IMapper mapper, IFileStorage storage,
        IModel3DGenerator generator, ILogger<Model3DRequestService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _storage = storage;
        _generator = generator;
        _logger = logger;
    }

    public async Task<IServiceResult<Model3DRequestQueueResponse>> GetQueueAsync(
        Model3DRequestStatus? status, Model3DFailureReason? reason, int skip, int take, CancellationToken ct = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 100);
        var (items, total, statusCounts) = await _uow.Products.GetStaffQueueAsync(status, reason, skip, take, ct);
        return ServiceResult<Model3DRequestQueueResponse>.Success(new Model3DRequestQueueResponse
        {
            Items = _mapper.Map<List<Model3DRequestQueueItemResponse>>(items),
            Total = total,
            StatusCounts = statusCounts.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
        });
    }

    public Task<IServiceResult<Model3DRequestQueueItemResponse>> GenerateAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct = default)
        => GenerateOrRetryAsync(requestId, staffUserId, body, isRetry: false, ct);

    public Task<IServiceResult<Model3DRequestQueueItemResponse>> RetryAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct = default)
        => GenerateOrRetryAsync(requestId, staffUserId, body, isRetry: true, ct);

    /// <summary>
    /// generate/retry cùng 1 hành vi: chọn ảnh → gửi Meshy → InProgress. Không có bước claim/khóa —
    /// bất kỳ staff sàn nào cũng gọi được, không giới hạn số lần (đã chốt trong ADR).
    /// </summary>
    private async Task<IServiceResult<Model3DRequestQueueItemResponse>> GenerateOrRetryAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, bool isRetry, CancellationToken ct)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null)
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);

        var validStatus = isRetry
            ? req.Status is Model3DRequestStatus.InProgress or Model3DRequestStatus.Failed
            : req.Status is Model3DRequestStatus.AwaitingStaff or Model3DRequestStatus.Failed;
        if (!validStatus)
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);

        if (req.ProductImageId is not { } targetImageId)
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(
                ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestTargetImageRequired);

        // Ảnh đích luôn phải nằm trong tập ảnh nguồn. Nếu staff không chọn lại ảnh thì dùng tập ảnh
        // đã được chủ cửa hàng gửi kèm request.
        var selectedIds = body.SourceImageIds is { Count: > 0 }
            ? body.SourceImageIds.Distinct().ToList()
            : req.SourceImageIds.Distinct().ToList();
        if (!selectedIds.Contains(targetImageId)) selectedIds.Insert(0, targetImageId);

        var effectiveBody = new RequestModel3DRequest
        {
            ProductImageId = targetImageId,
            SourceImageIds = selectedIds,
            NewImages = body.NewImages,
        };

        var (ids, urls, error) = await Model3DImageResolver.ResolveAsync(
            _uow, _storage, req.ProductId, effectiveBody, defaultToPrimaryIfEmpty: false, ct);
        if (error != Model3DImageResolver.ErrorCode.None)
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.BadRequest, MapImageError(error));

        req.SourceImageIds = ids;

        try
        {
            var taskId = await _generator.StartImageTo3DAsync(urls, ct);
            req.MeshyTaskId = taskId;
            req.AssignedStaffId = staffUserId;
            req.Status = Model3DRequestStatus.InProgress;
            req.InternalFailureReason = null;
            req.NextAttemptAt = null;
            req.RejectedReason = null;
        }
        catch (InsufficientCreditsException)
        {
            req.InternalFailureReason = Model3DFailureReason.InsufficientCredits;
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderInsufficientCredits);
        }
        catch (Model3DProviderException ex)
        {
            _logger.LogError(ex,
                "[Model3D] Meshy từ chối request {RequestId} của staff {StaffId}: HTTP {StatusCode} — {ProviderMessage}",
                requestId, staffUserId, ex.StatusCode, ex.ProviderMessage);

            var (statusCode, message) = MapProviderErrorForStaff(ex);
            req.InternalFailureReason = ex.StatusCode == 400
                ? Model3DFailureReason.InvalidImage
                : Model3DFailureReason.GenerationFailed;
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(statusCode, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Model3D] Staff {StaffId} gửi job Meshy thất bại cho request {RequestId}.", staffUserId, requestId);
            req.InternalFailureReason = Model3DFailureReason.GenerationFailed;
            await _uow.SaveChangesAsync(ct);
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderError);
        }

        await _uow.SaveChangesAsync(ct);
        return ServiceResult<Model3DRequestQueueItemResponse>.Success(
            _mapper.Map<Model3DRequestQueueItemResponse>(req), statusCode: ApiStatusCodes.Accepted);
    }

    public async Task<IServiceResult<Model3DPreviewResponse>> PreviewAsync(Guid requestId, CancellationToken ct = default)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null)
            return ServiceResult<Model3DPreviewResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);
        if (req.Status is not (Model3DRequestStatus.InProgress or Model3DRequestStatus.Failed)
            || string.IsNullOrWhiteSpace(req.MeshyTaskId))
            return ServiceResult<Model3DPreviewResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNoTaskToAccept);

        Model3DTaskResult result;
        try
        {
            result = await _generator.GetTaskAsync(req.MeshyTaskId, ct);
        }
        catch (Model3DProviderException ex)
        {
            _logger.LogWarning(ex, "[Model3D] Preview provider lỗi cho request {RequestId}: HTTP {StatusCode}.",
                requestId, ex.StatusCode);
            var (statusCode, message) = MapProviderErrorForStaff(ex);
            return ServiceResult<Model3DPreviewResponse>.Failure(statusCode, message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Model3D] Preview poll thất bại cho request {RequestId}.", requestId);
            return ServiceResult<Model3DPreviewResponse>.Failure(ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderError);
        }

        if (result.State == Model3DGenerationState.Failed && req.Status != Model3DRequestStatus.Failed)
        {
            req.Status = Model3DRequestStatus.Failed;
            req.InternalFailureReason = Model3DFailureReason.GenerationFailed;
            await _uow.SaveChangesAsync(ct);
        }

        return ServiceResult<Model3DPreviewResponse>.Success(new Model3DPreviewResponse
        {
            State = result.State.ToString(),
            Progress = result.Progress,
            ThumbnailUrl = result.ThumbnailUrl,
            GlbUrl = result.GlbUrl,
            Error = result.Error,
        });
    }

    public async Task<IServiceResult<Stream>> DownloadPreviewAsync(Guid requestId, CancellationToken ct = default)
    {
        // Resolve the current signed URL from the stored task, never from a caller-supplied URL.
        var preview = await PreviewAsync(requestId, ct);
        if (!preview.IsSuccess)
            return ServiceResult<Stream>.Failure(preview.StatusCode,
                preview.Message ?? ApiStatusMessages.Product.Model3DProviderError);

        if (preview.Data?.State != Model3DGenerationState.Succeeded.ToString()
            || string.IsNullOrWhiteSpace(preview.Data.GlbUrl))
            return ServiceResult<Stream>.Failure(ApiStatusCodes.Conflict,
                ApiStatusMessages.Product.Model3DRequestTaskNotSucceeded);

        try
        {
            var stream = await _generator.DownloadAsync(preview.Data.GlbUrl, ct);
            // Ownership passes to the controller's FileStreamResult.
            return ServiceResult<Stream>.Success(stream);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Model3D] Preview download failed for request {RequestId}.", requestId);
            return ServiceResult<Stream>.Failure(ApiStatusCodes.ServiceUnavailable,
                ApiStatusMessages.Product.Model3DProviderError);
        }
    }

    public async Task<IServiceResult<ProductModel3DResponse>> AcceptAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null)
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);
        if (req.Status != Model3DRequestStatus.InProgress || string.IsNullOrWhiteSpace(req.MeshyTaskId))
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNoTaskToAccept);

        Model3DTaskResult result;
        try
        {
            result = await _generator.GetTaskAsync(req.MeshyTaskId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Model3D] Accept: poll thất bại cho request {RequestId}.", requestId);
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderError);
        }

        if (result.State != Model3DGenerationState.Succeeded || string.IsNullOrWhiteSpace(result.GlbUrl))
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestTaskNotSucceeded);

        if (req.ProductImageId is not { } targetImageId)
            return ServiceResult<ProductModel3DResponse>.Failure(
                ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestTargetImageRequired);

        var existing = await _uow.Products.GetModel3DIncludingDeletedAsync(
            req.ProductId, targetImageId, ct);
        var oldModelUrl = existing?.ModelUrl;

        ProductModel3D model;
        if (existing is null)
        {
            model = new ProductModel3D { ProductId = req.ProductId, ProductImageId = targetImageId };
            await _uow.Products.AddModel3DAsync(model, ct);
        }
        else
        {
            model = existing;
            model.IsDeleted = false;
            model.ProductImageId = targetImageId;
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
            _logger.LogError(ex,
                "[Model3D] Accept: không thể lưu GLB vĩnh viễn cho product {ProductId}.",
                req.ProductId);
            return ServiceResult<ProductModel3DResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DStorageError);
        }

        model.Status = Model3DStatus.Succeeded;
        model.ErrorMessage = null;

        req.Status = Model3DRequestStatus.Succeeded;
        req.AssignedStaffId = staffUserId;

        await _uow.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(oldModelUrl) && oldModelUrl != model.ModelUrl)
            await _storage.DeleteByUrlAsync(oldModelUrl, ct);

        return ServiceResult<ProductModel3DResponse>.Success(
            _mapper.Map<ProductModel3DResponse>(model), ApiStatusMessages.Product.Model3DRequestAccepted);
    }

    public async Task<IServiceResult> RejectAsync(Guid requestId, Guid staffUserId, string reason, CancellationToken ct = default)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);
        if (req.Status is not (Model3DRequestStatus.AwaitingStaff
            or Model3DRequestStatus.InProgress or Model3DRequestStatus.Failed))
            return ServiceResult.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);

        reason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DRejectReasonRequired);
        if (reason.Length > 1000)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DRejectReasonTooLong);

        req.Status = Model3DRequestStatus.Rejected;
        req.RejectedReason = reason;
        req.AssignedStaffId = staffUserId;
        await _uow.SaveChangesAsync(ct);

        return ServiceResult.Success(ApiStatusMessages.Product.Model3DRequestRejected);
    }

    private static string MapImageError(Model3DImageResolver.ErrorCode error) => error switch
    {
        Model3DImageResolver.ErrorCode.ImageNotFound => ApiStatusMessages.Product.Model3DSourceImageNotFound,
        Model3DImageResolver.ErrorCode.ImageTypeInvalid => ApiStatusMessages.Product.ImageTypeInvalid,
        Model3DImageResolver.ErrorCode.TooManyImages => ApiStatusMessages.Product.Model3DImageLimitExceeded,
        _ => ApiStatusMessages.Product.Model3DImageRequired,
    };

    private static (int StatusCode, string Message) MapProviderErrorForStaff(Model3DProviderException error)
        => error.StatusCode switch
        {
            400 => (ApiStatusCodes.BadRequest,
                $"{ApiStatusMessages.Product.Model3DProviderInvalidRequest} Meshy: {error.ProviderMessage}"),
            401 or 403 => (ApiStatusCodes.ServiceUnavailable,
                ApiStatusMessages.Product.Model3DProviderUnauthorized),
            429 => (ApiStatusCodes.ServiceUnavailable,
                ApiStatusMessages.Product.Model3DProviderRateLimited),
            _ => (ApiStatusCodes.ServiceUnavailable,
                ApiStatusMessages.Product.Model3DProviderError),
        };
}
