using AutoMapper;
using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.Workspace.Mappings;

public class WorkspaceProfileMappingProfile : Profile
{
    public WorkspaceProfileMappingProfile()
    {
        CreateMap<Domain.Entities.Workspace.WorkspaceProfile, WorkspaceProfileResponse>()
            .ForMember(d => d.Images, opt => opt.MapFrom(s => s.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt)));
        CreateMap<Domain.Entities.Workspace.WorkspaceProfileImage, WorkspaceProfileImageResponse>();

        CreateMap<Domain.Entities.Workspace.WorkspaceType, WorkspaceTypeResponse>();

        CreateMap<CreateWorkspaceProfileRequest, Domain.Entities.Workspace.WorkspaceProfile>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.UserId, opt => opt.Ignore())
            .ForMember(d => d.User, opt => opt.Ignore())
            .ForMember(d => d.Images, opt => opt.Ignore());

        CreateMap<UpdateWorkspaceProfileRequest, Domain.Entities.Workspace.WorkspaceProfile>()
            .ForMember(d => d.Id, opt => opt.Ignore())
            .ForMember(d => d.UserId, opt => opt.Ignore())
            .ForMember(d => d.IsDefault, opt => opt.Ignore())
            .ForMember(d => d.User, opt => opt.Ignore())
            .ForMember(d => d.Images, opt => opt.Ignore());
    }
}
