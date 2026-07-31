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

        CreateMap<Ward, WardPathResponse>()
            .ForMember(d => d.WardId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.WardName, o => o.MapFrom(s => s.Name))
            .ForMember(d => d.DistrictId, o => o.MapFrom(s => s.DistrictId))
            .ForMember(d => d.DistrictName, o => o.MapFrom(s => s.District.Name))
            .ForMember(d => d.ProvinceId, o => o.MapFrom(s => s.District.ProvinceId))
            .ForMember(d => d.ProvinceName, o => o.MapFrom(s => s.District.Province.Name));

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
