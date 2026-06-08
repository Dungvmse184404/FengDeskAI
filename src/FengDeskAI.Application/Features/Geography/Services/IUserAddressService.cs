using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Geography.DTOs;

namespace FengDeskAI.Application.Features.Geography.Services;

public interface IUserAddressService
{
    Task<IServiceResult<List<UserAddressResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<IServiceResult<UserAddressResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult<UserAddressResponse>> CreateAsync(Guid userId, CreateUserAddressRequest request, CancellationToken ct = default);
    Task<IServiceResult<UserAddressResponse>> UpdateAsync(Guid id, Guid userId, UpdateUserAddressRequest request, CancellationToken ct = default);
    Task<IServiceResult<UserAddressResponse>> SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
