using BARD.Domain.Entities;
using BARD.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BARD.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent startup seeding: RBAC roles/permissions and localization
/// defaults. Safe to run on every application start — only inserts
/// rows that don't already exist, so administrator overrides
/// (terminology translations, role-permission changes made later via
/// the admin UI) are never touched or reset.
///
/// No production passwords or local accounts are created here. Role
/// ASSIGNMENT to a real Entra ID user is a separate administrative
/// action outside this seeder's scope, EXCEPT for the explicit
/// development-only test identity below, which is gated behind
/// configuration and clearly separated from production.
/// </summary>
public class DatabaseSeeder
{
    private readonly BardDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        BardDbContext db,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAndRolesAsync(ct);
        await SeedLocalizationAsync(ct);
        await SeedDevelopmentTestIdentityAsync(ct);
    }

    private async Task SeedPermissionsAndRolesAsync(CancellationToken ct)
    {
        var existingPermissionCodes = await _db.Permissions
            .Select(p => p.Code)
            .ToListAsync(ct);

        var permissionsByCode = await _db.Permissions
            .ToDictionaryAsync(p => p.Code, ct);

        foreach (var (code, description) in RbacSeedData.AllPermissions)
        {
            if (existingPermissionCodes.Contains(code))
                continue;

            var permission = Permission.Create(code, description);

            _db.Permissions.Add(permission);
            permissionsByCode[code] = permission;
        }

        await _db.SaveChangesAsync(ct);

        await EnsureRoleAsync(
            RbacSeedData.OfficerRoleCode,
            "Officer",
            "Processes and reviews excise refund dossiers.",
            RbacSeedData.OfficerPermissions,
            permissionsByCode,
            ct);

        await EnsureRoleAsync(
            RbacSeedData.AdministratorRoleCode,
            "Administrator",
            "Full administrative access, including Officer capabilities.",
            RbacSeedData.AdministratorPermissions,
            permissionsByCode,
            ct);

        _logger.LogInformation(
            "RBAC seed check complete ({PermissionCount} permissions, 2 roles).",
            RbacSeedData.AllPermissions.Length);
    }

    private async Task EnsureRoleAsync(
        string code,
        string name,
        string description,
        string[] permissionCodes,
        Dictionary<string, Permission> permissionsByCode,
        CancellationToken ct)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Code == code, ct);

        if (role is null)
        {
            role = Role.Create(code, name, description);

            _db.Roles.Add(role);
            await _db.SaveChangesAsync(ct);
        }

        var grantedPermissionIds = role.Permissions
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        foreach (var permissionCode in permissionCodes)
        {
            if (!permissionsByCode.TryGetValue(permissionCode, out var permission))
                continue;

            if (grantedPermissionIds.Contains(permission.Id))
                continue;

            role.GrantPermission(permission.Id);

            var newRolePermission = role.Permissions
                .FirstOrDefault(rp =>
                    rp.PermissionId == permission.Id &&
                    !grantedPermissionIds.Contains(rp.PermissionId));

            if (newRolePermission is not null)
            {
                _db.Entry(newRolePermission).State = EntityState.Added;
                grantedPermissionIds.Add(permission.Id);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedLocalizationAsync(CancellationToken ct)
    {
        var existingKeys = (
            await _db.LocalizationEntries
                .Select(e => e.Key)
                .ToListAsync(ct))
            .ToHashSet();

        var added = 0;

        foreach (var entry in LocalizationSeedData.GetEntries())
        {
            if (existingKeys.Contains(entry.Key))
                continue;

            _db.LocalizationEntries.Add(entry);
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Localization seed check complete ({Added} new key(s) inserted).",
            added);
    }

    /// <summary>
    /// Development-only test identities, clearly separated from
    /// production: only run when Development:SeedTestIdentity is
    /// explicitly true (never set in appsettings.Production.json).
    /// Seeds one Officer and one Administrator identity with fixed,
    /// well-known external ids so DevAuthenticationHandler (API layer,
    /// Codespaces-only) can impersonate either without any password or
    /// real Entra ID token ever being involved.
    /// </summary>
    public const string DevOfficerExternalId =
        "dev-officer-00000000-0000-0000-0000-000000000001";

    public const string DevAdministratorExternalId =
        "dev-admin-00000000-0000-0000-0000-000000000002";

    private async Task SeedDevelopmentTestIdentityAsync(CancellationToken ct)
    {
        var enabled = _configuration.GetValue<bool>(
            "Development:SeedTestIdentity");

        if (!enabled)
            return;

        var officerRole = await _db.Roles.FirstOrDefaultAsync(
            r => r.Code == RbacSeedData.OfficerRoleCode,
            ct);

        var adminRole = await _db.Roles.FirstOrDefaultAsync(
            r => r.Code == RbacSeedData.AdministratorRoleCode,
            ct);

        if (officerRole is null || adminRole is null)
        {
            _logger.LogWarning(
                "Officer/Administrator roles not found — run RBAC seeding before development identity seeding.");

            return;
        }

        var systemSeedUserId = Guid.Empty;

        await EnsureDevUserAsync(
            DevOfficerExternalId,
            "Dev Officer",
            "dev.officer@example.invalid",
            officerRole.Id,
            systemSeedUserId,
            ct);

        await EnsureDevUserAsync(
            DevAdministratorExternalId,
            "Dev Administrator",
            "dev.admin@example.invalid",
            adminRole.Id,
            systemSeedUserId,
            ct);

        _logger.LogWarning(
            "DEVELOPMENT-ONLY test identities seeded (Officer={OfficerId}, Administrator={AdminId}). " +
            "This must NEVER run against a production configuration.",
            DevOfficerExternalId,
            DevAdministratorExternalId);
    }

    private async Task EnsureDevUserAsync(
        string externalId,
        string displayName,
        string email,
        Guid roleId,
        Guid systemSeedUserId,
        CancellationToken ct)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(
            u => u.ExternalIdentityId == externalId,
            ct);

        if (existing is not null)
            return;

        var user = User.Create(
            externalId,
            displayName,
            email,
            systemSeedUserId);

        user.AssignRole(roleId, systemSeedUserId);

        _db.Users.Add(user);

        await _db.SaveChangesAsync(ct);
    }
}