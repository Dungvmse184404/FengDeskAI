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
        var (items, total) = await _uow.Products.GetStaffQueueAsync(status, reason, skip, take, ct);
        return ServiceResult<Model3DRequestQueueResponse>.Success(new Model3DRequestQueueResponse
        {
            Items = _mapper.Map<List<Model3DRequestQueueItemResponse>>(items),
            Total = total,
        });
    }

    public Task<IServiceResult<Model3DRequestQueueItemResponse>> GenerateAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct = default)
        => GenerateOrRetryAsync(requestId, staffUserId, body, ct);

    public Task<IServiceResult<Model3DRequestQueueItemResponse>> RetryAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct = default)
        => GenerateOrRetryAsync(requestId, staffUserId, body, ct);

    /// <summary>
    /// generate/retry cùng 1 hành vi: chọn ảnh → gửi Meshy → InProgress. Không có bước claim/khóa —
    /// bất kỳ staff sàn nào cũng gọi được, không giới hạn số lần (đã chốt trong ADR).
    /// </summary>
    private async Task<IServiceResult<Model3DRequestQueueItemResponse>> GenerateOrRetryAsync(
        Guid requestId, Guid staffUserId, RequestModel3DRequest body, CancellationToken ct)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null)
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);
        if (req.RequestType != Model3DRequestType.Regenerate)
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);
        if (req.Status is not (Model3DRequestStatus.AwaitingStaff or Model3DRequestStatus.InProgress))
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);

        var (ids, urls, error) = await Model3DImageResolver.ResolveAsync(
            _uow, _storage, req.ProductId, body, defaultToPrimaryIfEmpty: false, ct);
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
        }
        catch (InsufficientCreditsException)
        {
            // Khác Initial: đây là thao tác staff chủ động bấm → báo lỗi thật ngay, không âm thầm requeue.
            return ServiceResult<Model3DRequestQueueItemResponse>.Failure(
                ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderInsufficientCredits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Model3D] Staff {StaffId} gửi job Meshy thất bại cho request {RequestId}.", staffUserId, requestId);
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
        if (string.IsNullOrWhiteSpace(req.MeshyTaskId))
            return ServiceResult<Model3DPreviewResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNoTaskToAccept);

        Model3DTaskResult result;
        try
        {
            result = await _generator.GetTaskAsync(req.MeshyTaskId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Model3D] Preview poll thất bại cho request {RequestId}.", requestId);
            return ServiceResult<Model3DPreviewResponse>.Failure(ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderError);
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

    public async Task<IServiceResult<ProductModel3DResponse>> AcceptAsync(Guid requestId, Guid staffUserId, CancellationToken ct = default)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null)
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);
        if (req.RequestType != Model3DRequestType.Regenerate)
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);
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
                "[Model3D] Accept: re-host GLB sang storage thất bại cho product {ProductId} — tạm dùng URL provider.",
                req.ProductId);
            model.ModelUrl = result.GlbUrl;
        }

        model.Status = Model3DStatus.Succeeded;
        model.ErrorMessage = null;

        req.Status = Model3DRequestStatus.Succeeded;
        req.AssignedStaffId = staffUserId;

        await _uow.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(oldModelUrl))
            await _storage.DeleteByUrlAsync(oldModelUrl, ct);

        return ServiceResult<ProductModel3DResponse>.Success(
            _mapper.Map<ProductModel3DResponse>(model), ApiStatusMessages.Product.Model3DRequestAccepted);
    }

    public async Task<IServiceResult> RejectAsync(Guid requestId, Guid staffUserId, string reason, CancellationToken ct = default)
    {
        var req = await _uow.Products.GetModel3DRequestAsync(requestId, ct);
        if (req is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.Model3DRequestNotFound);
        if (req.RequestType != Model3DRequestType.Regenerate)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);
        if (req.Status is Model3DRequestStatus.Succeeded or Model3DRequestStatus.Rejected)
            return ServiceResult.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DRequestNotActionableByStaff);

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
}
