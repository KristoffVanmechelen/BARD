using BARD.Domain.Common;

namespace BARD.Domain.Entities;

public class User : AuditableEntity
{
    public string ExternalIdentityId { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string PreferredLanguage { get; private set; } = "nl-BE";
    public bool IsActive { get; private set; } = true;

    private readonly List<UserRoleAssignment> _roleAssignments = new();
    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    protected User() { }

    public static User Create(string externalIdentityId, string displayName, string email, Guid createdByUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            ExternalIdentityId = externalIdentityId,
            DisplayName = displayName,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };

    public void SetPreferredLanguage(string languageCode) => PreferredLanguage = languageCode;

    public void AssignRole(Guid roleId, Guid assignedByUserId) =>
        _roleAssignments.Add(UserRoleAssignment.Create(Id, roleId, assignedByUserId));
}

public class Role : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }

    private readonly List<RolePermission> _permissions = new();
    public IReadOnlyCollection<RolePermission> Permissions => _permissions.AsReadOnly();

    protected Role() { }

    public static Role Create(string code, string name, string? description = null) =>
        new() { Id = Guid.NewGuid(), Code = code, Name = name, Description = description };

    public void GrantPermission(Guid permissionId) => _permissions.Add(RolePermission.Create(Id, permissionId));
}

public class Permission : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;

    protected Permission() { }

    public static Permission Create(string code, string description) =>
        new() { Id = Guid.NewGuid(), Code = code, Description = description };
}

public class UserRoleAssignment : Entity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }

    protected UserRoleAssignment() { }

    public static UserRoleAssignment Create(Guid userId, Guid roleId, Guid assignedByUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            AssignedByUserId = assignedByUserId,
            AssignedAtUtc = DateTime.UtcNow,
        };
}

public class RolePermission : Entity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    protected RolePermission() { }

    public static RolePermission Create(Guid roleId, Guid permissionId) =>
        new() { Id = Guid.NewGuid(), RoleId = roleId, PermissionId = permissionId };
}

public static class PermissionCodes
{
    public const string TerminologyView = "terminology.view";
    public const string TerminologyEditCentral = "terminology.edit.central";
    public const string TerminologyInlineEdit = "terminology.edit.inline";
    public const string TerminologyViewHistory = "terminology.view.history";
    public const string TerminologyRestoreDefault = "terminology.restore.default";

    public const string ExciseRateView = "excise_rate.view";
    public const string ExciseRateEdit = "excise_rate.edit";
    public const string ExciseRateViewHistory = "excise_rate.view.history";

    public const string ConfigurationExport = "configuration.export";
    public const string ConfigurationImport = "configuration.import";
    public const string ConfigurationRestoreSnapshot = "configuration.restore_snapshot";

    public const string DossierView = "dossier.view";
    public const string DossierReview = "dossier.review";
    public const string DossierProcess = "dossier.process";
    public const string DossierAiAssist = "dossier.ai_assist";
}
