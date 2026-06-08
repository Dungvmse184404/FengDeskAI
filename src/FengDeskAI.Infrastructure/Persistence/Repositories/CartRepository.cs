using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class CartRepository : GenericRepository<Cart>, ICartRepository
{
    public CartRepository(AppDbContext context) : base(context) { }

    public Task<Cart?> GetByCustomerAsync(Guid customerId, CancellationToken ct = default)
        => _set.Include(c => c.Items).ThenInclude(i => i.ProductItem).ThenInclude(pi => pi.Product)
               .FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);

    public async Task<Cart> GetOrCreateAsync(Guid customerId, CancellationToken ct = default)
    {
        var cart = await _set.FirstOrDefaultAsync(c => c.CustomerId == customerId, ct);
        if (cart is null)
        {
            cart = new Cart { CustomerId = customerId };
            await _set.AddAsync(cart, ct);
        }
        return cart;
    }

    public Task<CartItem?> GetItemAsync(Guid cartId, Guid productItemId, CancellationToken ct = default)
        => _context.Set<CartItem>().FirstOrDefaultAsync(i => i.CartId == cartId && i.ProductItemId == productItemId, ct);

    public Task<CartItem?> GetItemByIdAsync(Guid cartId, Guid itemId, CancellationToken ct = default)
        => _context.Set<CartItem>().FirstOrDefaultAsync(i => i.CartId == cartId && i.Id == itemId, ct);

    public async Task AddItemAsync(CartItem item, CancellationToken ct = default)
        => await _context.Set<CartItem>().AddAsync(item, ct);

    public void RemoveItem(CartItem item) => _context.Set<CartItem>().Remove(item);

    public void RemoveItems(IEnumerable<CartItem> items) => _context.Set<CartItem>().RemoveRange(items);

    public Task<ProductItem?> GetProductItemAsync(Guid productItemId, CancellationToken ct = default)
        => _context.Set<ProductItem>().Include(pi => pi.Product)
               .FirstOrDefaultAsync(pi => pi.Id == productItemId, ct);
}
