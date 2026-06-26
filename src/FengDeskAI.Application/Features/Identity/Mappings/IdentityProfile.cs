using AutoMapper;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.Identity.Mappings;

public class IdentityProfile : Profile
{
    public IdentityProfile()
    {
        CreateMap<User, UserSummary>()
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()))
            .ForMember(d => d.Roles, opt => opt.MapFrom(s => s.Role.ToFlagList().Select(r => r.ToString()).ToList()))
            // Phong thủy tính sẵn từ ngày sinh + giới tính (FengShuiCalculator là nguồn chân lý chung
            // với engine chấm điểm). Giá trị phái sinh → không lưu DB, tính mỗi lần map.
            .ForMember(d => d.FengShui, opt => opt.MapFrom(s => BuildFengShui(s)));
    }

    private static UserFengShuiInfo? BuildFengShui(User user)
    {
        var profile = FengShuiCalculator.BuildPersonalProfile(user.DateOfBirth, user.Gender);
        if (profile is null)
            return null;

        return new UserFengShuiInfo
        {
            Element = profile.Element.ToString(),
            KuaNumber = profile.KuaNumber,
            KuaGroup = profile.Group?.ToString(),
            FavorableDirections = profile.FavorableDirections.Select(d => d.ToString()).ToList(),
        };
    }
}
