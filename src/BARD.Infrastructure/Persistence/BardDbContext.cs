using BARD.Application.Common.Interfaces;
using BARD.Domain.Common;
using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace BARD.Infrastructure.Persistence;

public class BardDbContext : DbContext, IApplicationDbContext
{
    public BardDbContext(DbContextOptions<BardDbContext> options) : base(options)
    {
    }

    public DbSet<Dossier> Dossiers => Set<Dossier>();
    public DbSet<DossierLine> DossierLines => Set<DossierLine>();
    public DbSet<DossierDocument> DossierDocuments => Set<DossierDocument>();
    public DbSet<ExtractedField> ExtractedFields => Set<ExtractedField>();
    public DbSet<DossierStatusHistoryEntry> DossierStatusHistory => Set<DossierStatusHistoryEntry>();
    public DbSet<Ac4Declaration> Ac4Declarations => Set<Ac4Declaration>();
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<ExciseRate> ExciseRates => Set<ExciseRate>();
    public DbSet<ExciseRateVersion> ExciseRateVersions => Set<ExciseRateVersion>();
    public DbSet<ExciseRateAuditEntry> ExciseRateAuditEntries => Set<ExciseRateAuditEntry>();

    public DbSet<LocalizationEntry> LocalizationEntries => Set<LocalizationEntry>();
    public DbSet<TerminologyOverride> TerminologyOverrides => Set<TerminologyOverride>();
    public DbSet<TerminologyAuditEntry> TerminologyAuditEntries => Set<TerminologyAuditEntry>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<ConfigurationSnapshot> ConfigurationSnapshots => Set<ConfigurationSnapshot>();
    public DbSet<ConfigurationOperationAuditEntry> ConfigurationOperationAuditEntries => Set<ConfigurationOperationAuditEntry>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Dossier>().HasQueryFilter(d => !d.IsDeleted);
        modelBuilder.Entity<DossierDocument>().HasQueryFilter(d => !d.IsDeleted);
        modelBuilder.Entity<ExciseRate>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<Company>().HasQueryFilter(c => !c.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}