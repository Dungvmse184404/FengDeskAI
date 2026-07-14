using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed bảng <c>element_input_map</c>: màu / vật liệu / hình khối → hành. Dùng chung phòng + sản phẩm.
/// Idempotent theo (kind, code, element).
/// </summary>
public class ElementInputMapSeeder : IDataSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<ElementInputMapSeeder> _logger;

    public ElementInputMapSeeder(AppDbContext context, ILogger<ElementInputMapSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int Order => 3;
    public string Name => "Element input map (color/material/shape → element)";

    // (kind, code, element, weight)
    private static readonly (ElementInputKind Kind, string Code, FengShuiElement Element, decimal Weight)[] Rows =
    {
        // ── Màu sắc ──
        (ElementInputKind.Color, "Red", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.Color, "Orange", FengShuiElement.Hoa, 0.7m),
        (ElementInputKind.Color, "Orange", FengShuiElement.Tho, 0.3m),
        (ElementInputKind.Color, "Purple", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.Color, "Pink", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.Color, "Blue", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.Color, "Black", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.Color, "White", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Color, "Gray", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Color, "Silver", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Color, "Green", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Color, "Brown", FengShuiElement.Tho, 0.6m),
        (ElementInputKind.Color, "Brown", FengShuiElement.Moc, 0.4m),
        (ElementInputKind.Color, "Yellow", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Color, "Beige", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Color, "Cream", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Color, "Gold", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Color, "Navy", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.Color, "Turquoise", FengShuiElement.Thuy, 0.6m),
        (ElementInputKind.Color, "Turquoise", FengShuiElement.Moc, 0.4m),
        (ElementInputKind.Color, "Maroon", FengShuiElement.Hoa, 0.7m),
        (ElementInputKind.Color, "Maroon", FengShuiElement.Tho, 0.3m),
        (ElementInputKind.Color, "Ivory", FengShuiElement.Tho, 0.6m),
        (ElementInputKind.Color, "Ivory", FengShuiElement.Kim, 0.4m),

        // ── Vật liệu ──
        (ElementInputKind.Material, "Wood", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Material, "Bamboo", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Material, "Paper", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Material, "Metal", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Material, "Brass", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Material, "Glass", FengShuiElement.Thuy, 0.6m),
        (ElementInputKind.Material, "Glass", FengShuiElement.Kim, 0.4m),
        (ElementInputKind.Material, "Crystal", FengShuiElement.Kim, 0.5m),
        (ElementInputKind.Material, "Crystal", FengShuiElement.Thuy, 0.5m),
        (ElementInputKind.Material, "Water", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.Material, "Ceramic", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Material, "Clay", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Material, "Stone", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Material, "SaltRock", FengShuiElement.Hoa, 0.6m),
        (ElementInputKind.Material, "SaltRock", FengShuiElement.Tho, 0.4m),
        (ElementInputKind.Material, "Candle", FengShuiElement.Hoa, 1.0m),
        // Vật liệu nội thất bổ sung — vd "bàn ghế chất liệu gỗ/da/vải".
        (ElementInputKind.Material, "Fabric", FengShuiElement.Moc, 0.6m),
        (ElementInputKind.Material, "Fabric", FengShuiElement.Hoa, 0.4m),
        (ElementInputKind.Material, "Leather", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Material, "Marble", FengShuiElement.Tho, 0.6m),
        (ElementInputKind.Material, "Marble", FengShuiElement.Kim, 0.4m),
        (ElementInputKind.Material, "Rattan", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Material, "Concrete", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Material, "Plastic", FengShuiElement.Kim, 0.5m),
        (ElementInputKind.Material, "Plastic", FengShuiElement.Tho, 0.5m),

        // ── Hình khối (chủ yếu dùng cho sản phẩm) ──
        (ElementInputKind.Shape, "Sphere", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Shape, "Round", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.Shape, "Wavy", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.Shape, "Column", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Shape, "Rectangular", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.Shape, "Triangle", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.Shape, "Pointed", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.Shape, "Square", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.Shape, "Flat", FengShuiElement.Tho, 1.0m),

        // ── Vật trang trí (chủ yếu dùng cho workspace — "hiện trạng phòng hiện tại") ──
        (ElementInputKind.DecorItem, "FishTank", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.DecorItem, "Fountain", FengShuiElement.Thuy, 1.0m),
        (ElementInputKind.DecorItem, "WindChime", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.DecorItem, "Mirror", FengShuiElement.Thuy, 0.7m),
        (ElementInputKind.DecorItem, "Mirror", FengShuiElement.Kim, 0.3m),
        (ElementInputKind.DecorItem, "Painting", FengShuiElement.Hoa, 0.5m),
        (ElementInputKind.DecorItem, "Painting", FengShuiElement.Moc, 0.5m),
        (ElementInputKind.DecorItem, "Rug", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.DecorItem, "Curtain", FengShuiElement.Moc, 0.6m),
        (ElementInputKind.DecorItem, "Curtain", FengShuiElement.Hoa, 0.4m),
        (ElementInputKind.DecorItem, "Lamp", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.DecorItem, "Clock", FengShuiElement.Kim, 1.0m),
        (ElementInputKind.DecorItem, "Plant", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.DecorItem, "Vase", FengShuiElement.Tho, 0.5m),
        (ElementInputKind.DecorItem, "Vase", FengShuiElement.Thuy, 0.5m),
        (ElementInputKind.DecorItem, "CrystalBall", FengShuiElement.Kim, 0.5m),
        (ElementInputKind.DecorItem, "CrystalBall", FengShuiElement.Thuy, 0.5m),
        (ElementInputKind.DecorItem, "IncenseBurner", FengShuiElement.Hoa, 0.6m),
        (ElementInputKind.DecorItem, "IncenseBurner", FengShuiElement.Tho, 0.4m),
        (ElementInputKind.DecorItem, "Bookshelf", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.DecorItem, "Bonsai", FengShuiElement.Moc, 1.0m),
        (ElementInputKind.DecorItem, "Lantern", FengShuiElement.Hoa, 1.0m),
        (ElementInputKind.DecorItem, "Statue", FengShuiElement.Tho, 1.0m),
        (ElementInputKind.DecorItem, "SaltLamp", FengShuiElement.Hoa, 0.5m),
        (ElementInputKind.DecorItem, "SaltLamp", FengShuiElement.Tho, 0.5m),
        (ElementInputKind.DecorItem, "Coins", FengShuiElement.Kim, 1.0m),
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var set = _context.Set<ElementInputMap>();
        var existing = (await set.Select(x => new { x.InputKind, x.InputCode, x.Element }).ToListAsync(ct))
            .Select(x => (x.InputKind, x.InputCode, x.Element)).ToHashSet();

        int added = 0;
        foreach (var (kind, code, element, weight) in Rows)
        {
            if (existing.Contains((kind, code, element))) continue;
            await set.AddAsync(new ElementInputMap
            {
                InputKind = kind,
                InputCode = code,
                Element = element,
                Weight = weight,
            }, ct);
            added++;
        }
        if (added > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seed element_input_map: thêm {Added} row.", added);
    }
}
