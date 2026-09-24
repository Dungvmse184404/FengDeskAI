using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Microsoft.Extensions.Caching.Memory;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bộ đếm "đời" của cache cấu hình chấm điểm. Singleton, nên mọi request dùng chung một con số.
/// Tăng số này là vô hiệu hoá toàn bộ khoá cũ trong một nhịp, không phải đi xoá từng khoá.
/// </summary>
public sealed class ScoringConfigCacheState : IScoringConfigCacheInvalidator
{
    private long _generation;

    public long Generation => Interlocked.Read(ref _generation);

    public void Invalidate() => Interlocked.Increment(ref _generation);
}

/// <summary>
/// Lớp bọc cache cho phần **cấu hình tĩnh** của engine chấm điểm.
///
/// <para>
/// Vì sao cần: mấy bảng này (tham số, map ngũ hành, vector loại phòng, modifier intent, hồ sơ nghề)
/// gần như không đổi, nhưng lại bị đọc lại ở MỌI request chấm điểm. DB nằm ở Sydney, mỗi lượt
/// round-trip ~300ms, nên riêng khối này đã ngốn hơn một giây của mỗi lần mở không gian làm việc,
/// trang chấm điểm sản phẩm và mỗi lượt gợi ý — để lấy về đúng vài chục dòng không bao giờ đổi.
/// </para>
///
/// <para>
/// CHỈ bọc các hàm đọc <c>AsNoTracking()</c>. Hai loại cố ý KHÔNG cache:
/// <list type="bullet">
/// <item>dữ liệu theo request (input của một phòng, input của sản phẩm) — cache vào là trả nhầm
/// phòng người khác;</item>
/// <item><see cref="GetOccupationByCodeAsync"/> — hàm này trả entity ĐANG ĐƯỢC THEO DÕI cho luồng
/// admin sửa nghề. Cache entity tracked là giữ lại tham chiếu tới một <c>DbContext</c> đã bị huỷ.</item>
/// </list>
/// </para>
///
/// ⚠ Danh sách trả ra là **dùng chung**, không phải bản sao. Người gọi phải coi nó là chỉ đọc —
/// sửa tại chỗ là đầu độc cache cho mọi request sau.
/// </summary>
public sealed class CachedScoringConfigRepository : IScoringConfigRepository
{
    /// <summary>
    /// TTL là lưới an toàn, không phải cơ chế chính — cơ chế chính là
    /// <see cref="IScoringConfigCacheInvalidator.Invalidate"/> gọi từ màn quản trị. TTL lo nốt
    /// trường hợp chạy nhiều instance: instance A sửa cấu hình thì cache của instance B không bị
    /// đụng tới, nên nó phải tự hết hạn.
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IScoringConfigRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly ScoringConfigCacheState _state;

    public CachedScoringConfigRepository(
        IScoringConfigRepository inner, IMemoryCache cache, ScoringConfigCacheState state)
    {
        _inner = inner;
        _cache = cache;
        _state = state;
    }

    /// <summary>Khoá có kèm "đời": đời tăng thì mọi khoá cũ thành mồ côi và tự rụng theo TTL.</summary>
    private Task<T> GetOrLoadAsync<T>(string key, Func<Task<T>> load)
        => _cache.GetOrCreateAsync($"scoring:{_state.Generation}:{key}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return load();
        })!;

    // ── Cấu hình tĩnh: có cache ──────────────────────────────────────────────────────────────

    public Task<List<ScoringParam>> GetScoringParamsAsync(CancellationToken ct = default)
        => GetOrLoadAsync("params", () => _inner.GetScoringParamsAsync(ct));

    public Task<List<ElementInputMap>> GetElementInputMapAsync(CancellationToken ct = default)
        => GetOrLoadAsync("element-input-map", () => _inner.GetElementInputMapAsync(ct));

    public Task<List<WorkspaceTypeElement>> GetWorkspaceTypeElementsAsync(
        Guid workspaceTypeId, CancellationToken ct = default)
        => GetOrLoadAsync($"type-elements:{workspaceTypeId}",
            () => _inner.GetWorkspaceTypeElementsAsync(workspaceTypeId, ct));

    public Task<List<WorkPurposeElementModifier>> GetWorkPurposeModifiersAsync(
        WorkPurpose purpose, CancellationToken ct = default)
        => GetOrLoadAsync($"work-purpose:{purpose}",
            () => _inner.GetWorkPurposeModifiersAsync(purpose, ct));

    public Task<List<OccupationElementProfile>> GetOccupationProfileAsync(
        Guid occupationId, CancellationToken ct = default)
        => GetOrLoadAsync($"occupation-profile:{occupationId}",
            () => _inner.GetOccupationProfileAsync(occupationId, ct));

    public Task<List<Occupation>> GetOccupationsAsync(
        bool includeInactive = false, CancellationToken ct = default)
        => GetOrLoadAsync($"occupations:{includeInactive}",
            () => _inner.GetOccupationsAsync(includeInactive, ct));

    // ── Theo request hoặc trả entity tracked: đi thẳng xuống DB ───────────────────────────────

    public Task<List<WorkspaceProfileInput>> GetWorkspaceProfileInputsAsync(
        Guid workspaceProfileId, CancellationToken ct = default)
        => _inner.GetWorkspaceProfileInputsAsync(workspaceProfileId, ct);

    public Task<List<ProductElementInput>> GetProductElementInputsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
        => _inner.GetProductElementInputsAsync(productIds, ct);

    public Task<Dictionary<Guid, IReadOnlyCollection<ProductElementInput>>> GetProductElementInputsByProductAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
        => _inner.GetProductElementInputsByProductAsync(productIds, ct);

    /// <summary>Trả entity ĐANG THEO DÕI cho luồng admin — không được cache. Xem ghi chú ở đầu lớp.</summary>
    public Task<Occupation?> GetOccupationByCodeAsync(string code, CancellationToken ct = default)
        => _inner.GetOccupationByCodeAsync(code, ct);

    public Task ReplaceProductElementInputsAsync(
        Guid productId, IEnumerable<ProductElementInput> inputs, CancellationToken ct = default)
        => _inner.ReplaceProductElementInputsAsync(productId, inputs, ct);

    public Task ReplaceWorkspaceProfileInputsAsync(
        Guid workspaceProfileId, IEnumerable<WorkspaceProfileInput> inputs, CancellationToken ct = default)
        => _inner.ReplaceWorkspaceProfileInputsAsync(workspaceProfileId, inputs, ct);
}
