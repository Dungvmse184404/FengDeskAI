using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;

namespace FengDeskAI.Application.Features.Sales.Services;

public interface ICartService
{
    Task<IServiceResult<CartResponse>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<IServiceResult<CartResponse>> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken ct = default);
    Task<IServiceResult<CartResponse>> UpdateItemAsync(Guid userId, Guid itemId, UpdateCartItemRequest request, CancellationToken ct = default);
    Task<IServiceResult<CartResponse>> RemoveItemAsync(Guid userId, Guid itemId, CancellationToken ct = default);
    Task<IServiceResult> ClearAsync(Guid userId, CancellationToken ct = default);
}
