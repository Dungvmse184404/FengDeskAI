using System.Globalization;
using System.Text;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;

namespace FengDeskAI.Application.Features.Identity.Services;

public class UserService : IUserService
{
    private const int MinQueryLength = 3;
    private const int DefaultLimit = 10;
    private const int MaxLimit = 20;

    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow) => _uow = uow;

    public async Task<IServiceResult<List<UserSearchResponse>>> SearchAsync(string? q, int? limit, CancellationToken ct = default)
    {
        var query = (q ?? string.Empty).Trim();
        if (query.Length < MinQueryLength)
            return ServiceResult<List<UserSearchResponse>>.Failure(
                ApiStatusCodes.BadRequest, ApiStatusMessages.UserSearch.QueryTooShort);

        var take = limit.GetValueOrDefault(DefaultLimit);
        if (take <= 0) take = DefaultLimit;
        if (take > MaxLimit) take = MaxLimit;

        var normalized = NormalizeForSearch(query);
        var users = await _uow.Users.SearchAsync(normalized, take, ct);

        var dtos = users.Select(u => new UserSearchResponse
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
        }).ToList();
        return ServiceResult<List<UserSearchResponse>>.Success(dtos);
    }

    /// <summary>
    /// Chuẩn hoá chuỗi search: lowercase + bỏ dấu Unicode + đ/Đ → d.
    /// Khớp với phép biến đổi SQL: unaccent(field).replace('đ','d').replace('Đ','d').ToLower().
    /// </summary>
    internal static string NormalizeForSearch(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var swapped = input.Replace('đ', 'd').Replace('Đ', 'd');
        var decomposed = swapped.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
