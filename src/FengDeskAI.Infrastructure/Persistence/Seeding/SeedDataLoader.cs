using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Đọc dữ liệu seed từ file JSON bên ngoài app (thư mục <c>seed-data/</c> ở gốc repo).
/// Đường dẫn ưu tiên: config <c>Seeding:DataPath</c> → <c>{BaseDirectory}/seed-data</c> (Docker)
/// → dò ngược thư mục cha từ cwd (chạy dev từ <c>src/FengDeskAI.WebAPI</c>).
/// Hệ số scale weight: file-level <c>weightScale</c> ghi đè config <c>Seeding:WeightScale</c> (mặc định 1.0).
/// </summary>
public class SeedDataLoader
{
    private readonly IConfiguration _config;
    private readonly ILogger<SeedDataLoader> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public SeedDataLoader(IConfiguration config, ILogger<SeedDataLoader> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>Hệ số scale mặc định toàn cục (config <c>Seeding:WeightScale</c>, mặc định 1.0).</summary>
    public decimal DefaultWeightScale => _config.GetValue<decimal?>("Seeding:WeightScale") ?? 1.0m;

    /// <summary>Load + deserialize 1 file JSON trong thư mục seed-data.</summary>
    public T Load<T>(string fileName)
    {
        var path = ResolvePath(fileName);
        _logger.LogInformation("Seed data: đọc {Path}", path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
               ?? throw new InvalidOperationException($"File seed data rỗng hoặc không hợp lệ: {path}");
    }

    /// <summary>Hệ số scale hiệu lực cho 1 file: file-level ghi đè global.</summary>
    public decimal EffectiveScale(decimal? fileScale) => fileScale ?? DefaultWeightScale;

    private string ResolvePath(string fileName)
    {
        var candidates = new List<string>();

        var configured = _config["Seeding:DataPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            candidates.Add(Path.Combine(configured, fileName));

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "seed-data", fileName));

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 5 && dir is not null; i++, dir = dir.Parent)
            candidates.Add(Path.Combine(dir.FullName, "seed-data", fileName));

        var found = candidates.FirstOrDefault(File.Exists);
        return found ?? throw new FileNotFoundException(
            $"Không tìm thấy file seed data '{fileName}'. Đã thử: {string.Join(" ; ", candidates.Distinct())}. " +
            "Đặt đường dẫn tuyệt đối qua config Seeding:DataPath nếu cần.");
    }
}

/// <summary>Bao ngoài chung cho file seed có weight: <c>{ "weightScale": 1.0, "rows": [...] }</c>.</summary>
public sealed class WeightedSeedFile<TRow>
{
    public decimal? WeightScale { get; set; }
    public List<TRow> Rows { get; set; } = new();
}
