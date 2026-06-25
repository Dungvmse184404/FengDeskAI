using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
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
            .Include(p => p.Elements)
            .Include(p => p.Vibes)
            .Include(p => p.Styles)
            .Include(p => p.Model3D)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
        => _set
            .Include(p => p.Items)
            .Include(p => p.Images)
            .Include(p => p.ProductCategories)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(List<Product> Items, int Total)> SearchAsync(ProductSearchFilter filter, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().AsQueryable();

        if (filter.ActiveOnly) query = query.Where(p => p.IsActive);
        if (filter.StoreId is { } storeId) query = query.Where(p => p.GardenStoreId == storeId);
        if (filter.CategoryId is { } categoryId)
            query = query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId));
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Tách query thành từng từ — MỖI từ phải khớp (AND) ở Name HOẶC Description.
            // Không phân biệt dấu + hoa thường (unaccent + ILIKE). VD "đèn ngủ" → SP phải chứa cả "đèn" lẫn "ngủ"
            // (theo bất kỳ thứ tự nào, ở tên hoặc mô tả), thay vì phải khớp nguyên cụm.
            var tokens = filter.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var pattern = $"%{token}%";
                query = query.Where(p =>
                    EF.Functions.ILike(AppDbContext.Unaccent(p.Name), AppDbContext.Unaccent(pattern))
                    || (p.Description != null
                        && EF.Functions.ILike(AppDbContext.Unaccent(p.Description), AppDbContext.Unaccent(pattern))));
            }
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

    public Task<List<Product>> GetScorableCandidatesAsync(CancellationToken ct = default)
        => _set.AsNoTracking()
            .Where(p => p.IsActive && p.Elements.Any())
            .Include(p => p.Elements)
            .Include(p => p.Vibes)
            .Include(p => p.Styles)
            .Include(p => p.Images)
            .Include(p => p.Items)
            .ToListAsync(ct);

    public Task<ProductItem?> GetItemAsync(Guid productId, Guid itemId, CancellationToken ct = default)
        => _context.Set<ProductItem>().FirstOrDefaultAsync(i => i.Id == itemId && i.ProductId == productId, ct);

    public async Task AddItemAsync(ProductItem item, CancellationToken ct = default)
        => await _context.Set<ProductItem>().AddAsync(item, ct);

    public void RemoveItem(ProductItem item) => _context.Set<ProductItem>().Remove(item);

    public Task<ProductImage?> GetImageAsync(Guid productId, Guid imageId, CancellationToken ct = default)
        => _context.Set<ProductImage>().FirstOrDefaultAsync(i => i.Id == imageId && i.ProductId == productId, ct);

    public Task<List<ProductImage>> ListImagesAsync(Guid productId, CancellationToken ct = default)
        => _context.Set<ProductImage>().AsNoTracking()
            .Where(i => i.ProductId == productId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync(ct);

    public async Task AddImageAsync(ProductImage image, CancellationToken ct = default)
        => await _context.Set<ProductImage>().AddAsync(image, ct);

    public void RemoveImage(ProductImage image) => _context.Set<ProductImage>().Remove(image);

    // ----- Model 3D (1–1 với product). Tracked để service/worker cập nhật trạng thái. -----

    public Task<ProductModel3D?> GetModel3DAsync(Guid productId, CancellationToken ct = default)
        => _context.Set<ProductModel3D>().FirstOrDefaultAsync(m => m.ProductId == productId, ct);

    public async Task AddModel3DAsync(ProductModel3D model, CancellationToken ct = default)
        => await _context.Set<ProductModel3D>().AddAsync(model, ct);

    public void RemoveModel3D(ProductModel3D model) => _context.Set<ProductModel3D>().Remove(model);

    public Task<List<ProductModel3D>> GetProcessingModel3DsAsync(CancellationToken ct = default)
        => _context.Set<ProductModel3D>()
            .Where(m => m.Status == Model3DStatus.Processing)
            .ToListAsync(ct);

    public async Task ReplaceCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds, CancellationToken ct = default)
    {
        var set = _context.Set<ProductCategory>();
        var existing = await set.Where(pc => pc.ProductId == productId).ToListAsync(ct);
        set.RemoveRange(existing);
        foreach (var cid in categoryIds.Distinct())
            await set.AddAsync(new ProductCategory { ProductId = productId, CategoryId = cid }, ct);
    }

    public async Task SetFengShuiAsync(Guid productId, FengShuiElement primary, IEnumerable<FengShuiElement> secondaries, SizeClass size, CancellationToken ct = default)
    {
        // size_class nằm trên products.
        var product = await _set.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is not null) product.SizeClass = size;

        // Thay toàn bộ hành: 1 hành chính (IsPrimary) + các hành phụ (khác hành chính).
        var set = _context.Set<ProductElement>();
        var existing = await set.Where(e => e.ProductId == productId).ToListAsync(ct);
        set.RemoveRange(existing);

        await set.AddAsync(new ProductElement { ProductId = productId, Element = primary, IsPrimary = true }, ct);
        foreach (var el in secondaries.Distinct().Where(e => e != primary))
            await set.AddAsync(new ProductElement { ProductId = productId, Element = el, IsPrimary = false }, ct);
    }

    public async Task ReplaceVibesAsync(Guid productId, IEnumerable<string> vibeCodes, CancellationToken ct = default)
    {
        var set = _context.Set<ProductVibe>();
        var existing = await set.Where(v => v.ProductId == productId).ToListAsync(ct);
        set.RemoveRange(existing);
        foreach (var code in vibeCodes.Distinct())
            await set.AddAsync(new ProductVibe { ProductId = productId, VibeCode = code }, ct);
    }

    public async Task ReplaceStylesAsync(Guid productId, IEnumerable<string> styleCodes, CancellationToken ct = default)
    {
        var set = _context.Set<ProductStyle>();
        var existing = await set.Where(s => s.ProductId == productId).ToListAsync(ct);
        set.RemoveRange(existing);
        foreach (var code in styleCodes.Distinct())
            await set.AddAsync(new ProductStyle { ProductId = productId, StyleCode = code }, ct);
    }
}
