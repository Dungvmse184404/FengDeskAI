using System.Reflection;
using System.Text.Json;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seed dữ liệu hành chính (tỉnh → quận → phường).
/// Nguồn dữ liệu, ưu tiên từ trên xuống:
///   1. File ngoài tại config <c>Seeding:GeographyDataPath</c> (nếu tồn tại) — dùng cho bộ đầy đủ 63 tỉnh.
///   2. JSON sample nhúng trong assembly (vietnam-geography.json).
/// Idempotent: chỉ seed khi bảng provinces đang rỗng.
/// </summary>
public class GeographySeeder : IDataSeeder
{
    private const string EmbeddedResourceSuffix = "vietnam-geography.json";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeographySeeder> _logger;

    public GeographySeeder(AppDbContext context, IConfiguration configuration, ILogger<GeographySeeder> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public int Order => 10;
    public string Name => "Geography (provinces/districts/wards)";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.Set<Province>().AnyAsync(ct))
        {
            _logger.LogInformation("Geography đã có dữ liệu — bỏ qua seeding.");
            return;
        }

        var json = await ReadSourceJsonAsync(ct);
        var provincesSeed = JsonSerializer.Deserialize<List<ProvinceSeed>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Không đọc được dữ liệu geography từ JSON.");

        var provinces = provincesSeed.Select(MapProvince).ToList();

        // Tắt change-tracking tự động để insert hàng loạt nhanh hơn
        var autoDetect = _context.ChangeTracker.AutoDetectChangesEnabled;
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await _context.Set<Province>().AddRangeAsync(provinces, ct);
            await _context.SaveChangesAsync(ct);
        }
        finally
        {
            _context.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }

        var districtCount = provinces.Sum(p => p.Districts.Count);
        var wardCount = provinces.Sum(p => p.Districts.Sum(d => d.Wards.Count));
        _logger.LogInformation("Seed geography xong: {Provinces} tỉnh, {Districts} quận/huyện, {Wards} phường/xã.",
            provinces.Count, districtCount, wardCount);
    }

    private static Province MapProvince(ProvinceSeed p) => new()
    {
        Name = p.Name,
        Code = p.Code,
        Districts = (p.Districts ?? new()).Select(d => new District
        {
            Name = d.Name,
            Code = d.Code,
            Wards = (d.Wards ?? new()).Select(w => new Ward
            {
                Name = w.Name,
                Code = w.Code,
            }).ToList(),
        }).ToList(),
    };

    private async Task<string> ReadSourceJsonAsync(CancellationToken ct)
    {
        var externalPath = _configuration["Seeding:GeographyDataPath"];
        if (!string.IsNullOrWhiteSpace(externalPath) && File.Exists(externalPath))
        {
            _logger.LogInformation("Đọc geography từ file ngoài: {Path}", externalPath);
            return await File.ReadAllTextAsync(externalPath, ct);
        }

        var assembly = typeof(GeographySeeder).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(EmbeddedResourceSuffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Không tìm thấy embedded resource '{EmbeddedResourceSuffix}'.");

        _logger.LogInformation("Đọc geography từ embedded sample ({Resource}).", resourceName);
        await using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }
}
