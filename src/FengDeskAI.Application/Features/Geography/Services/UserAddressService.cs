using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Geography.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Application.Features.Geography.Services;

public class UserAddressService : IUserAddressService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public UserAddressService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<List<UserAddressResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default)
    {
        var addresses = await _uow.UserAddresses.GetByUserIdAsync(userId, ct);
        return ServiceResult<List<UserAddressResponse>>.Success(_mapper.Map<List<UserAddressResponse>>(addresses));
    }

    public async Task<IServiceResult<UserAddressResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var address = await _uow.UserAddresses.GetByIdForUserAsync(id, userId, ct);
        if (address is null)
            return ServiceResult<UserAddressResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Address.NotFound);
        return ServiceResult<UserAddressResponse>.Success(_mapper.Map<UserAddressResponse>(address));
    }

    public async Task<IServiceResult<UserAddressResponse>> CreateAsync(Guid userId, CreateUserAddressRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request.WardId, request.RecipientName, request.RecipientPhone, request.StreetAddress, ct);
        if (validation is not null)
            return ServiceResult<UserAddressResponse>.Failure(ApiStatusCodes.BadRequest, validation);

        var entity = _mapper.Map<UserAddress>(request);
        entity.UserId = userId;

        var anyExisting = (await _uow.UserAddresses.GetByUserIdAsync(userId, ct)).Count > 0;
        if (request.IsDefault || !anyExisting)
        {
            await _uow.UserAddresses.ClearDefaultsForUserAsync(userId, ct);
            entity.IsDefault = true;
        }

        await _uow.UserAddresses.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<UserAddressResponse>.Success(
            _mapper.Map<UserAddressResponse>(entity), ApiStatusMessages.Address.Created, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<UserAddressResponse>> UpdateAsync(Guid id, Guid userId, UpdateUserAddressRequest request, CancellationToken ct = default)
    {
        var address = await _uow.UserAddresses.GetByIdForUserAsync(id, userId, ct);
        if (address is null)
            return ServiceResult<UserAddressResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Address.NotFound);

        var validation = await ValidateAsync(request.WardId, request.RecipientName, request.RecipientPhone, request.StreetAddress, ct);
        if (validation is not null)
            return ServiceResult<UserAddressResponse>.Failure(ApiStatusCodes.BadRequest, validation);

        _mapper.Map(request, address);
        _uow.UserAddresses.Update(address);
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<UserAddressResponse>.Success(_mapper.Map<UserAddressResponse>(address), ApiStatusMessages.Address.Updated);
    }

    public async Task<IServiceResult<UserAddressResponse>> SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var address = await _uow.UserAddresses.GetByIdForUserAsync(id, userId, ct);
        if (address is null)
            return ServiceResult<UserAddressResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Address.NotFound);

        return await _uow.ExecuteInTransactionAsync(async _ =>
        {
            await _uow.UserAddresses.ClearDefaultsForUserAsync(userId, ct);
            address.IsDefault = true;
            _uow.UserAddresses.Update(address);
            return ServiceResult<UserAddressResponse>.Success(_mapper.Map<UserAddressResponse>(address), ApiStatusMessages.Address.SetDefault);
        }, ct);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var address = await _uow.UserAddresses.GetByIdForUserAsync(id, userId, ct);
        if (address is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Address.NotFound);

        _uow.UserAddresses.Remove(address);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Address.Deleted);
    }

    private async Task<string?> ValidateAsync(Guid wardId, string recipientName, string recipientPhone, string street, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipientName)) return ApiStatusMessages.Address.RecipientNameRequired;
        if (string.IsNullOrWhiteSpace(recipientPhone)) return ApiStatusMessages.Address.RecipientPhoneRequired;
        if (string.IsNullOrWhiteSpace(street)) return ApiStatusMessages.Address.StreetRequired;
        if (!await _uow.Locations.WardExistsAsync(wardId, ct)) return ApiStatusMessages.Address.WardInvalid;
        return null;
    }
}
