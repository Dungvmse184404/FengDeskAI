using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Quản lý vector ngũ hành của sản phẩm (engine v3): khai input màu/vật liệu/hình khối (auto-calc),
/// hoặc ghi đè vector thủ công. Quyền: owner/staff của store sở hữu sản phẩm, hoặc Admin.
/// </summary>
public interface IProductVectorService
{
    Task<IServiceResult<ProductVectorResponse>> GetAsync(Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<IServiceResult<ProductVectorResponse>> SetElementInputsAsync(Guid productId, Guid userId, bool isAdmin, SetProductElementInputsRequest request, CancellationToken ct = default);
    Task<IServiceResult<ProductVectorResponse>> SetVectorOverrideAsync(Guid productId, Guid userId, bool isAdmin, SetProductVectorOverrideRequest request, CancellationToken ct = default);
    Task<IServiceResult<ProductVectorResponse>> ClearVectorOverrideAsync(Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default);
}
