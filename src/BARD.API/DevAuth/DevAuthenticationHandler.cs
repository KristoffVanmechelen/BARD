using System.Security.Claims;
using System.Text.Encodings.Web;
using BARD.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BARD.API.DevAuth;

/// <summary>
/// DEVELOPMENT-ONLY authentication scheme used exclusively in GitHub
/// Codespaces / local development, when Development:SeedTestIdentity is
/// true. Lets the browser select the seeded "Officer" or "Administrator"
/// identity via a request header, with NO password, NO real Entra ID
/// token, and NO production code path involved.
///
/// This scheme is registered in Program.cs ONLY when
/// Development:SeedTestIdentity is true — production configuration
/// never sets that flag, so this handler is never reachable in
/// production and Entra ID's JwtBearer scheme remains the only
/// authentication path there. See appsettings.Production.json, which
/// hardcodes SeedTestIdentity to false.
/// </summary>
public class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevelopmentTestIdentity";
    public const string RoleHeaderName = "X-Dev-Identity-Role";

    public DevAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeaderName, out var roleValues))
            return Task.FromResult(AuthenticateResult.Fail(
                $"Development authentication requires the '{RoleHeaderName}' header ('Officer' or 'Administrator')."));

        var role = roleValues.ToString();
        var externalId = role switch
        {
            "Officer" => DatabaseSeeder.DevOfficerExternalId,
            "Administrator" => DatabaseSeeder.DevAdministratorExternalId,
            _ => null,
        };

        if (externalId is null)
            return Task.FromResult(AuthenticateResult.Fail($"Unknown development identity role '{role}'."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, externalId),
            new Claim(ClaimTypes.Name, $"Dev {role}"),
            new Claim("dev_identity", "true"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
