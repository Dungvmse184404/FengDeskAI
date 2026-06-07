using FengDeskAI.Application.Features.Identity.Mappings;
using FengDeskAI.Application.Features.Identity.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace FengDeskAI.Application;

[ExcludeFromCodeCoverage]
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg =>
        {
            cfg.AddProfile<IdentityProfile>();
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistrationFlowService, RegistrationFlowService>();

        return services;
    }
}
