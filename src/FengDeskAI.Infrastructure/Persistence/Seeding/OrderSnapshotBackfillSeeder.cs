using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Điền cột chụp của <c>order_items</c> / <c>reviews</c> còn trống. Migration <c>AddOrderLineSnapshots</c> đã điền một
/// lần, nhưng CI chạy migrate TRƯỚC khi đổi container: đơn/đánh giá do container CŨ tạo trong khoảng đó chưa có cột
/// chụp. Seeder này chạy mỗi lần deploy, idempotent (chỉ đụng dòng còn trống) nên lần deploy sau vá nốt.
/// Đọc qua sản phẩm bằng SQL thô — bỏ qua bộ lọc xoá mềm, vì sản phẩm có thể đã bị người bán xoá mềm.
/// </summary>
public sealed class OrderSnapshotBackfillSeeder : IDataSeeder
{
    /// <summary>Dùng chung với migration — sửa ở đây thì xem lại migration.</summary>
    public const string OrderItemsSql = """
        UPDATE order_items oi
        SET product_id = pi.product_id,
            garden_store_id = p.garden_store_id,
            variant_name = pi.name,
            sku = pi.sku,
            image_url = (SELECT im.url FROM product_images im
                         WHERE im.product_id = p.id AND im.is_deleted = FALSE
                         ORDER BY im.sort_order, im.created_at LIMIT 1)
        FROM product_items pi
        JOIN products p ON p.id = pi.product_id
        WHERE pi.id = oi.product_item_id AND oi.product_id IS NULL;
        """;

    public const string ReviewsSql = """
        UPDATE reviews r
        SET product_name = p.name,
            garden_store_id = p.garden_store_id
        FROM products p
        WHERE p.id = r.product_id AND r.garden_store_id IS NULL;
        """;

    /// <summary>
    /// Gắn đánh giá cũ (chỉ có product_id) vào một dòng đơn của chính người viết, đơn mới nhất trước. Ngoặc trong
    /// chọn mỗi dòng đơn tối đa MỘT đánh giá, ngoặc ngoài mỗi đánh giá MỘT dòng đơn — không đụng UX_reviews_order_item.
    /// Đánh giá không truy được về đơn giữ order_item_id NULL.
    /// </summary>
    public const string ReviewOrderItemsSql = """
        UPDATE reviews r
        SET order_item_id = pick.order_item_id
        FROM (
            SELECT DISTINCT ON (c.review_id) c.review_id, c.order_item_id
            FROM (
                SELECT DISTINCT ON (oi.id) r2.id AS review_id, oi.id AS order_item_id, o.created_at AS ordered_at
                FROM reviews r2
                JOIN orders o ON o.customer_id = r2.user_id
                JOIN order_items oi ON oi.order_id = o.id AND oi.product_id = r2.product_id
                WHERE r2.order_item_id IS NULL AND r2.is_deleted = FALSE
                  AND NOT EXISTS (SELECT 1 FROM reviews r3
                                  WHERE r3.order_item_id = oi.id AND r3.is_deleted = FALSE)
                ORDER BY oi.id, r2.created_at
            ) c
            ORDER BY c.review_id, c.ordered_at DESC
        ) pick
        WHERE r.id = pick.review_id;
        """;

    private readonly AppDbContext _context;
    private readonly ILogger<OrderSnapshotBackfillSeeder> _logger;

    public OrderSnapshotBackfillSeeder(AppDbContext context, ILogger<OrderSnapshotBackfillSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 95;
    public string Name => "Điền cột chụp món trong đơn / đánh giá";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var items = await _context.Database.ExecuteSqlRawAsync(OrderItemsSql, ct);
        var reviews = await _context.Database.ExecuteSqlRawAsync(ReviewsSql, ct);
        var reviewLinks = await _context.Database.ExecuteSqlRawAsync(ReviewOrderItemsSql, ct);
        if (items + reviews + reviewLinks > 0)
            _logger.LogInformation("Snapshot backfill: {Items} món trong đơn, {Reviews} đánh giá, {Links} đánh giá gắn dòng đơn.",
                items, reviews, reviewLinks);
    }
}
