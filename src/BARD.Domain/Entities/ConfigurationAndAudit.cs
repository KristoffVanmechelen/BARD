using BARD.Domain.Common;

namespace BARD.Domain.Entities;

public class ConfigurationSnapshot : Entity
{
    public string ConfigurationFormatVersion { get; private set; } = default!;
    public string ApplicationVersion { get; private set; } = default!;
    public string SnapshotJson { get; private set; } = default!;
    public string Reason { get; private set; } = default!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected ConfigurationSnapshot() { }

    public static ConfigurationSnapshot Create(string formatVersion, string appVersion, string snapshotJson,
        string reason, Guid createdByUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            ConfigurationFormatVersion = formatVersion,
            ApplicationVersion = appVersion,
            SnapshotJson = snapshotJson,
            Reason = reason,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
        };
}

public class ConfigurationOperationAuditEntry : Entity
{
    public string OperationType { get; private set; } = default!;
    public Guid UserId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string ConfigurationFormatVersion { get; private set; } = default!;
    public string ImportedOrExportedSections { get; private set; } = default!;
    public bool Success { get; private set; }
    public string? ValidationErrors { get; private set; }
    public Guid? SnapshotId { get; private set; }

    protected ConfigurationOperationAuditEntry() { }

    public static ConfigurationOperationAuditEntry Create(string operationType, Guid userId,
        string formatVersion, string sections, bool success, string? validationErrors, Guid? snapshotId) =>
        new()
        {
            Id = Guid.NewGuid(),
            OperationType = operationType,
            UserId = userId,
            TimestampUtc = DateTime.UtcNow,
            ConfigurationFormatVersion = formatVersion,
            ImportedOrExportedSections = sections,
            Success = success,
            ValidationErrors = validationErrors,
            SnapshotId = snapshotId,
        };
}

public class AuditLogEntry : Entity
{
    public string EntityType { get; private set; } = default!;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = default!;
    public string? Details { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime TimestampUtc { get; private set; }

    protected AuditLogEntry() { }

    public static AuditLogEntry Create(string entityType, Guid entityId, string action, string? details, Guid? userId) =>
        new()
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Details = details,
            UserId = userId,
            TimestampUtc = DateTime.UtcNow,
        };
}
