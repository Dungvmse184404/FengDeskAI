using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Gắn thẻ MỤC TIÊU phong thủy (<see cref="Aspiration"/>) cho sản phẩm demo — data đọc từ
/// <c>product-aspirations-demo.json</c>, không hardcode.
/// <para>
/// Không có thẻ nào được duyệt thì bộ lọc mục tiêu ("tôi muốn sản phẩm hỗ trợ tiền tài") luôn rơi vào
/// nhánh fallback bỏ lọc — dev/demo không bao giờ thấy tính năng chạy thật. Seeder này tồn tại để
/// có dữ liệu mồi.
/// </para>
/// Idempotent theo cặp <c>(product_id, aspiration)</c>: chỉ INSERT dòng còn thiếu, KHÔNG đụng dòng đã
/// có — nên chạy lại bao nhiêu lần cũng an toàn, kể cả trên DB đã có admin duyệt/bỏ duyệt bằng tay.
/// </summary>
public class ProductAspirationDemoSeeder : IDataSeeder
{
    private const string FileName = "product-aspirations-demo.json";

    private readonly AppDbContext _context;
    private readonly SeedDataLoader _loader;
    private readonly ILogger<ProductAspirationDemoSeeder> _logger;

    public ProductAspirationDemoSeeder(
        AppDbContext context, SeedDataLoader loader, ILogger<ProductAspirationDemoSeeder> logger)
    {
        _context = context;
        _loader = loader;
        _logger = logger;
    }

    public int Order => 24; // sau PlacementProductDemoSeeder (23) — cần sản phẩm Carry đã tồn tại
    public string Name => "Product aspirations demo (feng shui goals)";

    public sealed class FileModel
    {
        /// <summary>
        /// true → seed thẳng ở trạng thái ĐÃ DUYỆT (dev/demo lọc được ngay).
        /// false → chỉ là đề xuất, engine bỏ qua cho tới khi admin duyệt qua API.
        /// </summary>
        public bool ApproveOnSeed { get; set; } = true;

        public List<Row> Rows { get; set; } = new();
    }

    public sealed class Row
    {
        /// <summary>Chuỗi con trong tên sản phẩm, không phân biệt hoa/thường (vd "Kim Tiền").</summary>
        public string Match { get; set; } = "";

        /// <summary>Wealth | Career | Health | Relationship | Study.</summary>
        public List<string> Aspirations { get; set; } = new();
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var file = _loader.Load<FileModel>(FileName);
        if (file.Rows.Count == 0) return;

        var products = await _context.Set<Product>()
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);
        if (products.Count == 0)
        {
            _logger.LogInformation("Chưa có sản phẩm nào — bỏ qua seed thẻ mục tiêu.");
            return;
        }

        // Cặp đã tồn tại → không đụng (giữ nguyên trạng thái duyệt admin đã đặt bằng tay).
        var existing = (await _context.Set<ProductAspiration>()
                .Select(a => new { a.ProductId, a.Aspiration })
                .ToListAsync(ct))
            .Select(x => (x.ProductId, x.Aspiration))
            .ToHashSet();

        var toAdd = new List<ProductAspiration>();
        foreach (var row in file.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Match)) continue;

            var matched = products
                .Where(p => p.Name.Contains(row.Match, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matched.Count == 0)
            {
                _logger.LogInformation("Không sản phẩm nào khớp '{Match}' — bỏ qua.", row.Match);
                continue;
            }

            foreach (var code in row.Aspirations)
            {
                if (!Enum.TryParse<Aspiration>(code, ignoreCase: true, out var aspiration))
                {
                    _logger.LogWarning("{File}: mục tiêu '{Code}' không hợp lệ — bỏ qua.", FileName, code);
                    continue;
                }

                foreach (var p in matched)
                {
                    if (!existing.Add((p.Id, aspiration))) continue; // đã có → bỏ qua

                    toAdd.Add(new ProductAspiration
                    {
                        ProductId = p.Id,
                        Aspiration = aspiration,
                        IsApproved = file.ApproveOnSeed,
                        // ApprovedBy để null: đây là data seed, không phải quyết định của một admin cụ thể.
                        ApprovedAt = file.ApproveOnSeed ? DateTime.UtcNow : null,
                    });
                }
            }
        }

        if (toAdd.Count == 0)
        {
            _logger.LogInformation("Thẻ mục tiêu demo đã đầy đủ — bỏ qua seeding.");
            return;
        }

        await _context.Set<ProductAspiration>().AddRangeAsync(toAdd, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seed {Count} thẻ mục tiêu cho sản phẩm demo (approved={Approved}).",
            toAdd.Count, file.ApproveOnSeed);
    }
}
