using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public Task<Product?> GetDetailAsync(Guid id, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Include(p => p.Store)
            .Include(p => p.Items)
            .Include(p => p.Images)
            .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
            .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
        => _set
            .Include(p => p.Items)
            .Include(p => p.Images)
            .Include(p => p.ProductCategories)
            .Include(p => p.ProductTags)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(List<Product> Items, int Total)> SearchAsync(ProductSearchFilter filter, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().AsQueryable();

        if (filter.ActiveOnly) query = query.Where(p => p.IsActive);
        if (filter.StoreId is { } storeId) query = query.Where(p => p.GardenStoreId == storeId);
        if (filter.CategoryId is { } categoryId)
            query = query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId));
        if (filter.TagId is { } tagId)
            query = query.Where(p => p.ProductTags.Any(pt => pt.TagId == tagId));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip(filter.Skip).Take(filter.Take)
            .Include(p => p.Items)
            .Include(p => p.Images)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<ProductItem?> GetItemAsync(Guid productId, Guid itemId, CancellationToken ct = default)
        => _context.Set<ProductItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.ProductId == productId, ct);

    public async Task AddItemAsync(ProductItem item, CancellationToken ct = default)
        => await _context.Set<ProductItem>().AddAsync(item, ct);

    public void RemoveItem(ProductItem item) => _context.Set<ProductItem>().Remove(item);

    public Task<ProductImage?> GetImageAsync(Guid productId, Guid imageId, CancellationToken ct = default)
        => _context.Set<ProductImage>().FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);

    public async Task AddImageAsync(ProductImage image, CancellationToken ct = default)
        => await _context.Set<ProductImage>().AddAsync(image, ct);

    public void RemoveImage(ProductImage image) => _context.Set<ProductImage>().Remove(image);

    public async Task ReplaceCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds, CancellationToken ct = default)
    {
        var set = _context.Set<ProductCategory>();
        var existing = await set.Where(pc => pc.ProductId == productId).ToListAsync(ct);
        set.RemoveRange(existing);
        foreach (var cid in categoryIds.Distinct())
            await set.AddAsync(new ProductCategory { ProductId = productId, CategoryId = cid }, ct);
    }

    public async Task ReplaceTagsAsync(Guid productId, IEnumerable<Guid> tagIds, CancellationToken ct = default)
    {
        var set = _context.Set<ProductTag>();
        var existing = await set.Where(pt => pt.ProductId == productId).ToListAsync(ct);
        set.RemoveRange(existing);
        foreach (var tid in tagIds.Distinct())
            await set.AddAsync(new ProductTag { ProductId = productId, TagId = tid }, ct);
    }
}
