using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface ICartRepository : IGenericRepository<Cart>
{
    /// <summary>Giỏ của user kèm Items → ProductItem → Product (tracked, để hiển thị + checkout).</summary>
    Task<Cart?> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);

    /// <summary>Lấy giỏ hiện có hoặc tạo mới (chưa SaveChanges).</summary>
    Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken ct = default);

    Task<CartItem?> GetItemAsync(Guid cartId, Guid productItemId, CancellationToken ct = default);
    Task<CartItem?> GetItemByIdAsync(Guid cartId, Guid itemId, CancellationToken ct = default);
    Task AddItemAsync(CartItem item, CancellationToken ct = default);
    void RemoveItem(CartItem item);
    void RemoveItems(IEnumerable<CartItem> items);

    /// <summary>ProductItem kèm Product (lấy giá/tồn/IsActive/store) để validate.</summary>
    Task<ProductItem?> GetProductItemAsync(Guid productItemId, CancellationToken ct = default);

    /// <summary>Nhiều ProductItem (tracked) kèm Product — dùng cho checkout mua-ngay theo danh sách.</summary>
    Task<List<ProductItem>> GetProductItemsAsync(IEnumerable<Guid> productItemIds, CancellationToken ct = default);
}
