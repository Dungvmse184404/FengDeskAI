using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FengDeskAI.Application.Features.Geography.Services;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Infrastructure.ExternalServices.Shipping;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.Persistence.Seeding;

/// <summary>
/// Đồng bộ dữ liệu hành chính VN (open-api.vn) + mã GHN. Xem Documents/GHN_INTEGRATION.md §10.
/// Bước A: upsert cây tỉnh/quận/phường theo Code (idempotent). Bước B: khớp với master-data GHN
/// (tỉnh/quận theo Code, fallback tên; phường theo tên trong quận đã khớp) rồi điền các cột Ghn*.
/// </summary>
public class GeoSyncService : IGeoSyncService
{
    public const string OpenApiClient = "OpenApiGeo";
    public const string GhnMasterDataClient = "GhnMasterData";
    private const string OpenApiTreePath = "/api/v1/?depth=3"; // cấu trúc cũ (tỉnh→quận→phường), khớp master-data GHN

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDbContext _ctx;
    private readonly IHttpClientFactory _httpFactory;
    private readonly GhnSettings _ghn;
    private readonly ILogger<GeoSyncService> _logger;

    public GeoSyncService(AppDbContext ctx, IHttpClientFactory httpFactory,
        IOptions<GhnSettings> ghn, ILogger<GeoSyncService> logger)
    {
        _ctx = ctx;
        _httpFactory = httpFactory;
        _ghn = ghn.Value;
        _logger = logger;
    }

    // ===================== Bước A: nạp dữ liệu hành chính =====================

    public async Task<GeoSyncReport> ImportGovernmentDataAsync(CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient(OpenApiClient);
        _logger.LogInformation("[GeoSync] Bước A: tải cây hành chính từ {Base}{Path}…", http.BaseAddress, OpenApiTreePath);

        var json = await http.GetStringAsync(OpenApiTreePath, ct);
        var tree = JsonSerializer.Deserialize<List<ProvinceSeed>>(json, JsonOpts)
            ?? throw new InvalidOperationException("Không đọc được dữ liệu hành chính từ open-api.vn.");

        var pByCode = (await _ctx.Set<Province>().ToListAsync(ct)).ToDictionary(p => p.Code);
        var dByCode = (await _ctx.Set<District>().ToListAsync(ct)).ToDictionary(d => d.Code);
        var wByCode = (await _ctx.Set<Ward>().ToListAsync(ct)).ToDictionary(w => w.Code);

        int addedP = 0, addedD = 0, addedW = 0;
        var autoDetect = _ctx.ChangeTracker.AutoDetectChangesEnabled;
        _ctx.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var p in tree)
            {
                var prov = Upsert(pByCode, p.Code, () => new Province { Code = p.Code }, ref addedP, _ctx);
                prov.Name = p.Name;

                foreach (var d in p.Districts ?? new())
                {
                    var dist = Upsert(dByCode, d.Code, () => new District { Code = d.Code }, ref addedD, _ctx);
                    dist.Name = d.Name;
                    dist.ProvinceId = prov.Id;

                    foreach (var w in d.Wards ?? new())
                    {
                        var ward = Upsert(wByCode, w.Code, () => new Ward { Code = w.Code }, ref addedW, _ctx);
                        ward.Name = w.Name;
                        ward.DistrictId = dist.Id;
                    }
                }
            }
            _ctx.ChangeTracker.DetectChanges();
            await _ctx.SaveChangesAsync(ct);
        }
        finally
        {
            _ctx.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
        }

        var report = new GeoSyncReport(pByCode.Count, dByCode.Count, wByCode.Count, 0);
        _logger.LogInformation("[GeoSync] Bước A xong: {P} tỉnh ({Ap} mới), {D} quận ({Ad} mới), {W} phường ({Aw} mới).",
            report.Provinces, addedP, report.Districts, addedD, report.Wards, addedW);
        return report;
    }

    private static T Upsert<T>(Dictionary<int, T> byCode, int code, Func<T> create, ref int added, AppDbContext ctx)
        where T : class
    {
        if (byCode.TryGetValue(code, out var existing)) return existing;
        var entity = create();
        ctx.Add(entity);
        byCode[code] = entity;
        added++;
        return entity;
    }

    // ===================== Bước B: khớp mã GHN =====================

    public async Task<GeoSyncReport> SyncGhnCodesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_ghn.Token))
        {
            _logger.LogWarning("[GeoSync] Bước B bỏ qua: chưa cấu hình Ghn:Token.");
            return GeoSyncReport.Empty;
        }

        var http = _httpFactory.CreateClient(GhnMasterDataClient);

        var provinces = await _ctx.Set<Province>()
            .Include(p => p.Districts).ThenInclude(d => d.Wards)
            .ToListAsync(ct);

        var ghnProvinces = await GetAsync<GhnProvince>(http, HttpMethod.Get, "/shiip/public-api/master-data/province", null, ct);
        // Tỉnh: GHN "Code" là mã nội bộ (vd "4" cho Hà Nội) ≠ mã GSO → CHỈ khớp theo tên (+ NameExtension).
        var ghnProvByName = BuildNameIndex(ghnProvinces, x => x.ProvinceName, x => x.NameExtension);

        int matchedP = 0, matchedD = 0, matchedW = 0, unmatchedD = 0, unmatchedW = 0;

        foreach (var prov in provinces)
        {
            if (!ghnProvByName.TryGetValue(Norm(prov.Name), out var gp))
            {
                _logger.LogWarning("[GeoSync] Không khớp tỉnh GHN: {Name} ({Code}).", prov.Name, prov.Code);
                continue;
            }
            prov.GhnProvinceId = gp.ProvinceId;
            matchedP++;

            try
            {
                var ghnDistricts = await GetAsync<GhnDistrict>(http, HttpMethod.Post,
                    "/shiip/public-api/master-data/district", new { province_id = gp.ProvinceId }, ct);
                // Quận: khớp theo GovernmentCode (mã GSO) trước, fallback theo tên.
                var dByGov = BuildCodeIndex(ghnDistricts, x => x.GovernmentCode);
                var dByName = BuildNameIndex(ghnDistricts, x => x.DistrictName, x => x.NameExtension);

                foreach (var dist in prov.Districts)
                {
                    var gd = Resolve(dist.Code, dist.Name, dByGov, dByName);
                    if (gd is null) { unmatchedD++; _logger.LogWarning("[GeoSync] Không khớp quận GHN: {Name} ({Code}) / {Prov}.", dist.Name, dist.Code, prov.Name); continue; }
                    dist.GhnDistrictId = gd.DistrictId;
                    matchedD++;

                    var ghnWards = await GetAsync<GhnWard>(http, HttpMethod.Post,
                        "/shiip/public-api/master-data/ward", new { district_id = gd.DistrictId }, ct);
                    var wByGov = BuildCodeIndex(ghnWards, x => x.GovernmentCode);
                    var wByName = BuildNameIndex(ghnWards, x => x.WardName, x => x.NameExtension);

                    foreach (var ward in dist.Wards)
                    {
                        // Phường: GovernmentCode (mã GSO) nếu có, fallback theo tên trong quận đã khớp.
                        var gw = wByGov.TryGetValue(ward.Code, out var byGov) ? byGov
                               : wByName.TryGetValue(Norm(ward.Name), out var byName) ? byName : null;
                        if (gw is not null) { ward.GhnWardCode = gw.WardCode; matchedW++; }
                        else { unmatchedW++; }
                    }
                }

                // Lưu sau mỗi tỉnh: giữ tiến độ, một tỉnh lỗi không làm mất toàn bộ.
                await _ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GeoSync] Lỗi khi đồng bộ tỉnh {Name} — bỏ qua, tiếp tục tỉnh khác.", prov.Name);
            }
        }

        _logger.LogInformation("[GeoSync] Bước B xong: khớp {P} tỉnh, {D} quận, {W} phường. Chưa khớp: {Ud} quận, {Uw} phường.",
            matchedP, matchedD, matchedW, unmatchedD, unmatchedW);
        if (unmatchedD > 0) _logger.LogWarning("[GeoSync] {Ud} quận chưa có GhnDistrictId — nghi sai Code, cần map tay.", unmatchedD);
        if (unmatchedW > 0) _logger.LogWarning("[GeoSync] {Uw} phường chưa có GhnWardCode — thường do lệch tên (cải cách 2025), map tay/override.", unmatchedW);

        return new GeoSyncReport(matchedP, matchedD, matchedW, unmatchedD + unmatchedW);
    }

    // ===================== Khớp & chuẩn hóa =====================

    /// <summary>Khớp theo gov Code (chính xác) trước, fallback theo tên đã chuẩn hóa.</summary>
    private static T? Resolve<T>(int ourCode, string ourName, Dictionary<int, T> byCode, Dictionary<string, T> byName)
        where T : class
        => byCode.TryGetValue(ourCode, out var c) ? c
         : byName.TryGetValue(Norm(ourName), out var n) ? n
         : null;

    private static Dictionary<int, T> BuildCodeIndex<T>(List<T> items, Func<T, string?> code)
    {
        var dict = new Dictionary<int, T>();
        foreach (var i in items)
            if (int.TryParse(code(i), NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                dict[v] = i;
        return dict;
    }

    private static Dictionary<string, T> BuildNameIndex<T>(List<T> items, Func<T, string> name, Func<T, List<string>?> aliases)
    {
        var dict = new Dictionary<string, T>();
        foreach (var i in items)
        {
            dict[Norm(name(i))] = i;
            foreach (var alias in aliases(i) ?? new())
                dict.TryAdd(Norm(alias), i);
        }
        return dict;
    }

    private async Task<List<T>> GetAsync<T>(HttpClient http, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var msg = new HttpRequestMessage(method, path);
        if (body is not null) msg.Content = JsonContent.Create(body);
        using var res = await http.SendAsync(msg, ct);
        res.EnsureSuccessStatusCode();
        var env = await res.Content.ReadFromJsonAsync<GhnEnvelope<T>>(cancellationToken: ct);
        return env?.Data ?? new List<T>();
    }

    private static readonly string[] Prefixes =
        { "thanh pho ", "tinh ", "quan ", "huyen ", "thi xa ", "thi tran ", "phuong ", "xa ", "tp ", "tp." };

    /// <summary>Bỏ dấu, lowercase, gộp khoảng trắng, bỏ tiền tố hành chính — để so khớp tên hai phía.</summary>
    private static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var formD = s.Trim().ToLowerInvariant().Replace("đ", "d").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var t = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), "\\s+", " ").Trim();
        foreach (var p in Prefixes)
            if (t.StartsWith(p, StringComparison.Ordinal)) { t = t[p.Length..].Trim(); break; }
        return t;
    }
}
