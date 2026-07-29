using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BARD.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", schema: "identity");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.ExternalIdentityId).HasMaxLength(200).IsRequired();
        builder.HasIndex(u => u.ExternalIdentityId).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.PreferredLanguage).HasMaxLength(10);

        builder.HasMany(u => u.RoleAssignments)
            .WithOne()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(u => u.RoleAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", schema: "identity");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.Code).IsUnique();
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1000);

        builder.HasMany(r => r.Permissions)
            .WithOne()
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Permissions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", schema: "identity");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(500).IsRequired();
    }
}

public class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.ToTable("UserRoleAssignments", schema: "identity");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => new { a.UserId, a.RoleId }).IsUnique();
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions", schema: "identity");
        builder.HasKey(rp => rp.Id);
        builder.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
    }
}

public class ConfigurationSnapshotConfiguration : IEntityTypeConfiguration<ConfigurationSnapshot>
{
    public void Configure(EntityTypeBuilder<ConfigurationSnapshot> builder)
    {
        builder.ToTable("ConfigurationSnapshots", schema: "audit");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ConfigurationFormatVersion).HasMaxLength(20).IsRequired();
        builder.Property(s => s.ApplicationVersion).HasMaxLength(50).IsRequired();
        builder.Property(s => s.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(s => s.Reason).HasMaxLength(500);
    }
}

public class ConfigurationOperationAuditEntryConfiguration : IEntityTypeConfiguration<ConfigurationOperationAuditEntry>
{
    public void Configure(EntityTypeBuilder<ConfigurationOperationAuditEntry> builder)
    {
        builder.ToTable("ConfigurationOperationAuditEntries", schema: "audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.OperationType).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ConfigurationFormatVersion).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ImportedOrExportedSections).HasColumnType("nvarchar(max)");
        builder.Property(a => a.ValidationErrors).HasColumnType("nvarchar(max)");
    }
}

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries", schema: "audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Details).HasColumnType("nvarchar(max)");
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
        builder.HasIndex(a => a.TimestampUtc);
    }
}
