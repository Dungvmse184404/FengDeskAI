using AutoMapper;
using FengDeskAI.Application.Features.Geography.DTOs;
using FengDeskAI.Domain.Entities.Geography;

namespace FengDeskAI.Application.Features.Geography.Mappings;

public class GeographyMappingProfile : Profile
{
    public GeographyMappingProfile()
    {
        CreateMap<Province, ProvinceResponse>();
        CreateMap<District, DistrictResponse>();
        CreateMap<Ward, WardResponse>();

        CreateMap<UserAddress, UserAddressResponse>();

        CreateMap<CreateUserAddressRequest, UserAddress>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.UserId, opt => opt.Ignore())
            .ForMember(d => d.Ward, opt => opt.Ignore());

        CreateMap<UpdateUserAddressRequest, UserAddress>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.UserId, opt => opt.Ignore())
            .ForMember(d => d.IsDefault, opt => opt.Ignore())
            .ForMember(d => d.Ward, opt => opt.Ignore());
    }
}
