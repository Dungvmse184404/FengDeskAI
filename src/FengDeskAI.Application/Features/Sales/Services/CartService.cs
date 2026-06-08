using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Sales.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public CartService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<CartResponse>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);
        return ServiceResult<CartResponse>.Success(BuildResponse(userId, cart));
    }

    public async Task<IServiceResult<CartResponse>> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.BadRequest, "Số lượng phải lớn hơn 0.");

        var productItem = await _uow.Carts.GetProductItemAsync(request.ProductItemId, ct);
        if (productItem is null || productItem.Product is null || !productItem.Product.IsActive)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.NotFound, "Sản phẩm không tồn tại hoặc ngừng bán.");

        var cart = await _uow.Carts.GetOrCreateAsync(userId, ct);
        var existing = await _uow.Carts.GetItemAsync(cart.Id, request.ProductItemId, ct);
        var newQuantity = (existing?.Quantity ?? 0) + request.Quantity;

        if (newQuantity > productItem.Stock)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.BadRequest, $"Không đủ tồn kho (còn {productItem.Stock}).");

        if (existing is null)
        {
            await _uow.Carts.AddItemAsync(new CartItem
            {
                CartId = cart.Id,
                ProductItemId = request.ProductItemId,
                Quantity = request.Quantity,
                AddedAt = DateTime.UtcNow,
            }, ct);
        }
        else
        {
            existing.Quantity = newQuantity;
        }

        await _uow.SaveChangesAsync(ct);
        return await GetMineAsync(userId, ct);
    }

    public async Task<IServiceResult<CartResponse>> UpdateItemAsync(Guid userId, Guid itemId, UpdateCartItemRequest request, CancellationToken ct = default)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);
        if (cart is null)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.NotFound, "Giỏ hàng trống.");

        var item = await _uow.Carts.GetItemByIdAsync(cart.Id, itemId, ct);
        if (item is null)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy dòng hàng trong giỏ.");

        if (request.Quantity <= 0)
        {
            _uow.Carts.RemoveItem(item);
        }
        else
        {
            var productItem = await _uow.Carts.GetProductItemAsync(item.ProductItemId, ct);
            if (productItem is not null && request.Quantity > productItem.Stock)
                return ServiceResult<CartResponse>.Failure(ApiStatusCodes.BadRequest, $"Không đủ tồn kho (còn {productItem.Stock}).");
            item.Quantity = request.Quantity;
        }

        await _uow.SaveChangesAsync(ct);
        return await GetMineAsync(userId, ct);
    }

    public async Task<IServiceResult<CartResponse>> RemoveItemAsync(Guid userId, Guid itemId, CancellationToken ct = default)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);
        if (cart is null)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.NotFound, "Giỏ hàng trống.");

        var item = await _uow.Carts.GetItemByIdAsync(cart.Id, itemId, ct);
        if (item is null)
            return ServiceResult<CartResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy dòng hàng trong giỏ.");

        _uow.Carts.RemoveItem(item);
        await _uow.SaveChangesAsync(ct);
        return await GetMineAsync(userId, ct);
    }

    public async Task<IServiceResult> ClearAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await _uow.Carts.GetByCustomerAsync(userId, ct);
        if (cart is not null && cart.Items.Count > 0)
        {
            _uow.Carts.RemoveItems(cart.Items);
            await _uow.SaveChangesAsync(ct);
        }
        return ServiceResult.Success("Đã xóa toàn bộ giỏ hàng.");
    }

    private CartResponse BuildResponse(Guid userId, Cart? cart)
    {
        if (cart is null)
            return new CartResponse { CustomerId = userId };

        var items = _mapper.Map<List<CartItemResponse>>(cart.Items.OrderBy(i => i.AddedAt));
        return new CartResponse
        {
            Id = cart.Id,
            CustomerId = cart.CustomerId,
            Items = items,
            Subtotal = items.Sum(i => i.LineTotal),
        };
    }
}
