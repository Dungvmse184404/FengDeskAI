using AutoMapper;
using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Application.Features.Vendor.Mappings;

public class VendorMappingProfile : Profile
{
    public VendorMappingProfile()
    {
        CreateMap<StoreAddress, StoreAddressResponse>();
        CreateMap<GardenStore, StoreResponse>();
        CreateMap<GardenStaffAssignment, StaffAssignmentResponse>();

        CreateMap<CreateStoreRequest, GardenStore>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.Address, opt => opt.Ignore())
            .ForMember(d => d.StaffAssignments, opt => opt.Ignore());
    }
}
