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

    public async Task<IServiceResult<ProductModel3DResponse>> GenerateAsync(
        Guid productId, Guid userId, bool isAdmin, GenerateModel3DRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.ManageForbidden);

        var existing = await _uow.Products.GetModel3DAsync(productId, ct);
        if (existing is { Status: Model3DStatus.Processing })
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DAlreadyProcessing);

        // Chọn ảnh nguồn: chỉ định cụ thể hoặc ảnh primary (SortOrder nhỏ nhất).
        ProductImage? sourceImage;
        if (request.SourceImageId is { } imageId)
        {
            sourceImage = await _uow.Products.GetImageAsync(productId, imageId, ct);
            if (sourceImage is null)
                return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DSourceImageNotFound);
        }
        else
        {
            var images = await _uow.Products.ListImagesAsync(productId, ct);
            sourceImage = images.OrderBy(i => i.SortOrder).FirstOrDefault();
            if (sourceImage is null)
                return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.Model3DSourceImageRequired);
        }

        string taskId;
        try
        {
            taskId = await _generator.StartImageTo3DAsync(sourceImage.Url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Model3D] Gửi job Meshy thất bại cho product {ProductId}.", productId);
            return ServiceResult<ProductModel3DResponse>.Failure(ApiStatusCodes.ServiceUnavailable, ApiStatusMessages.Product.Model3DProviderError);
        }

        // File GLB cũ (nếu sinh lại) → xóa best-effort sau khi cập nhật.
        var oldModelUrl = existing?.ModelUrl;

        ProductModel3D model;
        if (existing is null)
        {
            model = new ProductModel3D { ProductId = productId };
            await _uow.Products.AddModel3DAsync(model, ct);
        }
        else
        {
            model = existing;
        }

        model.Status = Model3DStatus.Processing;
        model.SourceImageUrl = sourceImage.Url;
        model.MeshyTaskId = taskId;
        model.Progress = 0;
        model.ModelUrl = null;
        model.ThumbnailUrl = null;
        model.ErrorMessage = null;

        await _uow.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(oldModelUrl))
            await _storage.DeleteByUrlAsync(oldModelUrl, ct);

        return ServiceResult<ProductModel3DResponse>.Success(
            _mapper.Map<ProductModel3DResponse>(model), ApiStatusMessages.Product.Model3DStarted, ApiStatusCodes.Accepted);
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

    public async Task PollPendingAsync(CancellationToken ct = default)
    {
        var pending = await _uow.Products.GetProcessingModel3DsAsync(ct);
        if (pending.Count == 0) return;

        var changed = false;
        foreach (var model in pending)
        {
            if (string.IsNullOrWhiteSpace(model.MeshyTaskId))
            {
                model.Status = Model3DStatus.Failed;
                model.ErrorMessage = "Thiếu task id.";
                changed = true;
                continue;
            }

            Model3DTaskResult result;
            try
            {
                result = await _generator.GetTaskAsync(model.MeshyTaskId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Model3D] Poll task {TaskId} thất bại — thử lại lượt sau.", model.MeshyTaskId);
                continue;
            }

            model.Progress = result.Progress;

            switch (result.State)
            {
                case Model3DGenerationState.Running:
                    changed = true; // cập nhật progress
                    break;

                case Model3DGenerationState.Failed:
                    model.Status = Model3DStatus.Failed;
                    model.ErrorMessage = result.Error ?? "Sinh model 3D thất bại.";
                    changed = true;
                    break;

                case Model3DGenerationState.Succeeded:
                    await FinalizeSucceededAsync(model, result, ct);
                    changed = true;
                    break;
            }
        }

        if (changed) await _uow.SaveChangesAsync(ct);
    }

    /// <summary>Tải GLB từ provider rồi re-host sang storage. Lỗi storage → fallback URL provider để vẫn dùng được.</summary>
    private async Task FinalizeSucceededAsync(ProductModel3D model, Model3DTaskResult result, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(result.GlbUrl))
        {
            model.Status = Model3DStatus.Failed;
            model.ErrorMessage = "Provider báo hoàn tất nhưng không có file GLB.";
            return;
        }

        model.ThumbnailUrl = result.ThumbnailUrl;
        model.Progress = 100;

        try
        {
            await using var src = await _generator.DownloadAsync(result.GlbUrl, ct);
            using var buffer = new MemoryStream();
            await src.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            var objectPath = $"Product_models/{model.ProductId}/{Guid.NewGuid():N}.glb";
            var stored = await _storage.UploadAsync(objectPath, buffer, GlbContentType, ct);
            model.ModelUrl = stored.Url;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Model3D] Re-host GLB sang storage thất bại cho product {ProductId} — tạm dùng URL provider.",
                model.ProductId);
            model.ModelUrl = result.GlbUrl; // fallback: URL tạm của provider
        }

        model.Status = Model3DStatus.Succeeded;
    }

    private async Task<bool> CanManageStoreAsync(Guid storeId, Guid userId, bool isAdmin, CancellationToken ct)
        => isAdmin || await _uow.Stores.CanManageAsync(storeId, userId, ct);
}
