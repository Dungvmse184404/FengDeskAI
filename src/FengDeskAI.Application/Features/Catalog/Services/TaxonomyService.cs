using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.Extensions.Caching.Memory;

namespace FengDeskAI.Application.Features.Catalog.Services;

public sealed class TaxonomyService : ITaxonomyService
{
    private const string ElementInputCodesCacheKey = "element-input-codes";
    private static readonly TimeSpan ElementInputCodesCacheTtl = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;

    public TaxonomyService(IUnitOfWork uow, IMemoryCache cache)
    {
        _uow = uow;
        _cache = cache;
    }

    // ── Styles ──
    public Task<IServiceResult<List<LookupItemResponse>>> GetStylesAsync(bool includeInactive, CancellationToken ct = default)
        => ListAsync(_uow.Styles, includeInactive, ct);

    public Task<IServiceResult<LookupItemResponse>> CreateStyleAsync(CreateLookupRequest request, CancellationToken ct = default)
        => CreateAsync(_uow.Styles, request, "Phong cách", ct);

    public Task<IServiceResult<LookupItemResponse>> UpdateStyleAsync(string code, UpdateLookupRequest request, CancellationToken ct = default)
        => UpdateAsync(_uow.Styles, code, request, "phong cách", ct);

    // ── Vibes ──
    public Task<IServiceResult<List<LookupItemResponse>>> GetVibesAsync(bool includeInactive, CancellationToken ct = default)
        => ListAsync(_uow.Vibes, includeInactive, ct);

    public Task<IServiceResult<LookupItemResponse>> CreateVibeAsync(CreateLookupRequest request, CancellationToken ct = default)
        => CreateAsync(_uow.Vibes, request, "Vibe", ct);

    public Task<IServiceResult<LookupItemResponse>> UpdateVibeAsync(string code, UpdateLookupRequest request, CancellationToken ct = default)
        => UpdateAsync(_uow.Vibes, code, request, "vibe", ct);

    // ── Elements (chỉ đọc + sửa tên) ──
    public Task<IServiceResult<List<LookupItemResponse>>> GetElementsAsync(bool includeInactive, CancellationToken ct = default)
        => ListAsync(_uow.Elements, includeInactive, ct);

    public Task<IServiceResult<LookupItemResponse>> UpdateElementAsync(string code, UpdateLookupRequest request, CancellationToken ct = default)
        => UpdateAsync(_uow.Elements, code, request, "hành", ct);

    // ── Element input codes (vocabulary cho form vendor, cache 10') ──
    public async Task<IServiceResult<List<ElementInputCodesResponse>>> GetElementInputCodesAsync(CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(ElementInputCodesCacheKey, out List<ElementInputCodesResponse>? cached) || cached is null)
        {
            var map = await _uow.ScoringConfig.GetElementInputMapAsync(ct);
            cached = map
                .GroupBy(m => m.InputKind)
                .Select(g => new ElementInputCodesResponse { Kind = g.Key, Codes = g.Select(m => m.InputCode).Distinct().ToList() })
                .ToList();
            _cache.Set(ElementInputCodesCacheKey, cached, ElementInputCodesCacheTtl);
        }

        return ServiceResult<List<ElementInputCodesResponse>>.Success(cached);
    }

    // ── Generic dùng chung cho mọi bảng tra cứu ──
    private static async Task<IServiceResult<List<LookupItemResponse>>> ListAsync<T>(
        IGenericRepository<T> repo, bool includeInactive, CancellationToken ct) where T : class, ILookup
    {
        var all = await repo.GetAllAsync(ct);
        var items = all
            .Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(Map)
            .ToList();
        return ServiceResult<List<LookupItemResponse>>.Success(items);
    }

    private async Task<IServiceResult<LookupItemResponse>> CreateAsync<T>(
        IGenericRepository<T> repo, CreateLookupRequest request, string label, CancellationToken ct)
        where T : class, ILookup, new()
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return ServiceResult<LookupItemResponse>.Failure(ApiStatusCodes.BadRequest, "Code không được để trống.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<LookupItemResponse>.Failure(ApiStatusCodes.BadRequest, "Tên không được để trống.");

        var code = request.Code.Trim();
        if (await repo.GetByIdAsync(code, ct) is not null)
            return ServiceResult<LookupItemResponse>.Failure(ApiStatusCodes.Conflict, $"{label} '{code}' đã tồn tại.");

        var entity = new T { Code = code, Name = request.Name.Trim(), IsActive = true, SortOrder = request.SortOrder };
        await repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<LookupItemResponse>.Success(Map(entity), $"Đã thêm {label.ToLower()}.", ApiStatusCodes.Created);
    }

    private async Task<IServiceResult<LookupItemResponse>> UpdateAsync<T>(
        IGenericRepository<T> repo, string code, UpdateLookupRequest request, string label, CancellationToken ct)
        where T : class, ILookup
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<LookupItemResponse>.Failure(ApiStatusCodes.BadRequest, "Tên không được để trống.");

        var entity = await repo.GetByIdAsync(code, ct);
        if (entity is null)
            return ServiceResult<LookupItemResponse>.Failure(ApiStatusCodes.NotFound, $"Không tìm thấy {label} '{code}'.");

        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        repo.Update(entity);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<LookupItemResponse>.Success(Map(entity), $"Đã cập nhật {label}.");
    }

    private static LookupItemResponse Map(ILookup x) => new(x.Code, x.Name, x.IsActive, x.SortOrder);
}
