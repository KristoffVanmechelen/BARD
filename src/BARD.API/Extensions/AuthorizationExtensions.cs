using BARD.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace BARD.API.Extensions;

/// <summary>
/// Fine-grained permission requirement, per decision #15: access to an
/// operational screen never implicitly grants terminology/other rights
/// — every capability is checked against its own explicit permission code.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUser;

    public PermissionAuthorizationHandler(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (_currentUser.HasPermission(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class AuthorizationExtensions
{
    /// <summary>
    /// Registers one authorization policy per permission code defined in
    /// PermissionCodes, so controllers can [Authorize(Policy = PermissionCodes.X)].
    /// </summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(
                Domain.Entities.PermissionCodes.TerminologyView,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.TerminologyView)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.TerminologyEditCentral,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.TerminologyEditCentral)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.TerminologyInlineEdit,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.TerminologyInlineEdit)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.TerminologyViewHistory,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.TerminologyViewHistory)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.TerminologyRestoreDefault,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.TerminologyRestoreDefault)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.ExciseRateView,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.ExciseRateView)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.ExciseRateEdit,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.ExciseRateEdit)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.ExciseRateViewHistory,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.ExciseRateViewHistory)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.ConfigurationExport,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.ConfigurationExport)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.ConfigurationImport,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.ConfigurationImport)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.ConfigurationRestoreSnapshot,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.ConfigurationRestoreSnapshot)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.DossierView,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.DossierView)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.DossierReview,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.DossierReview)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.DossierProcess,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.DossierProcess)))
            .AddPolicy(
                Domain.Entities.PermissionCodes.DossierAiAssist,
                p => p.Requirements.Add(
                    new PermissionRequirement(
                        Domain.Entities.PermissionCodes.DossierAiAssist)));

        return services;
    }
}