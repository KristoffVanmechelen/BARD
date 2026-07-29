using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Dossier> Dossiers { get; }
    DbSet<DossierLine> DossierLines { get; }
    DbSet<DossierDocument> DossierDocuments { get; }
    DbSet<ExtractedField> ExtractedFields { get; }
    DbSet<DossierStatusHistoryEntry> DossierStatusHistory { get; }
    DbSet<Ac4Declaration> Ac4Declarations { get; }
    DbSet<Company> Companies { get; }

    DbSet<ExciseRate> ExciseRates { get; }
    DbSet<ExciseRateVersion> ExciseRateVersions { get; }
    DbSet<ExciseRateAuditEntry> ExciseRateAuditEntries { get; }

    DbSet<LocalizationEntry> LocalizationEntries { get; }
    DbSet<TerminologyOverride> TerminologyOverrides { get; }
    DbSet<TerminologyAuditEntry> TerminologyAuditEntries { get; }

    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRoleAssignment> UserRoleAssignments { get; }
    DbSet<RolePermission> RolePermissions { get; }

    DbSet<ConfigurationSnapshot> ConfigurationSnapshots { get; }
    DbSet<ConfigurationOperationAuditEntry> ConfigurationOperationAuditEntries { get; }
    DbSet<AuditLogEntry> AuditLogEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUserService
{
    Guid UserId { get; }
    string DisplayName { get; }
    string PreferredLanguage { get; }
    bool HasPermission(string permissionCode);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}

public interface IBlobStorageService
{
    Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
}

public interface IAuditLogger
{
    Task LogAsync(string entityType, Guid entityId, string action, object? details, CancellationToken ct = default);
}
