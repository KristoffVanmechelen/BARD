using BARD.Domain.Entities;

namespace BARD.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed data for the initial RBAC roles/permissions (authoritative
/// decision: "Keep the existing Microsoft Entra ID integration
/// architecture... Seed roles, permissions and role-permission mappings
/// only"). Two initial roles: Officer, Administrator. No local
/// production accounts or passwords are created — role ASSIGNMENT to a
/// specific Entra ID user remains a separate administrative action
/// (see RbacSeeder's DevelopmentTestIdentity remarks for the
/// dev-only exception).
/// </summary>
public static class RbacSeedData
{
    public const string OfficerRoleCode = "Officer";
    public const string AdministratorRoleCode = "Administrator";

    public static readonly (string Code, string Description)[] AllPermissions =
    {
        (PermissionCodes.TerminologyView, "View terminology entries"),
        (PermissionCodes.TerminologyEditCentral, "Edit terminology via Settings > Terminology"),
        (PermissionCodes.TerminologyInlineEdit, "Edit terminology via inline 'Edit texts' mode"),
        (PermissionCodes.TerminologyViewHistory, "View terminology change history"),
        (PermissionCodes.TerminologyRestoreDefault, "Restore terminology to default values"),

        (PermissionCodes.ExciseRateView, "View excise rates"),
        (PermissionCodes.ExciseRateEdit, "Create, edit, activate/deactivate excise rates"),
        (PermissionCodes.ExciseRateViewHistory, "View excise rate change history"),

        (PermissionCodes.ConfigurationExport, "Export application configuration"),
        (PermissionCodes.ConfigurationImport, "Import application configuration"),
        (PermissionCodes.ConfigurationRestoreSnapshot, "Restore a configuration snapshot"),

        (PermissionCodes.DossierView, "View dossiers and their lines"),
        (PermissionCodes.DossierReview, "Approve or reject dossier lines"),
        (PermissionCodes.DossierProcess, "Upload and process a new dossier"),
        (PermissionCodes.DossierAiAssist, "Trigger on-demand AI-assisted extraction"),
    };

    /// <summary>
    /// Officer: the day-to-day operational role — process and review
    /// dossiers, read-only on Terminology/Excise Rates (per decision
    /// #15: viewing an operational screen never implies edit rights).
    /// </summary>
    public static readonly string[] OfficerPermissions =
    {
        PermissionCodes.DossierView,
        PermissionCodes.DossierReview,
        PermissionCodes.DossierProcess,
        PermissionCodes.DossierAiAssist,
        PermissionCodes.TerminologyView,
        PermissionCodes.ExciseRateView,
    };

    /// <summary>Administrator: full access, including all Officer capabilities.</summary>
    public static readonly string[] AdministratorPermissions =
        AllPermissions.Select(p => p.Code).ToArray();
}
