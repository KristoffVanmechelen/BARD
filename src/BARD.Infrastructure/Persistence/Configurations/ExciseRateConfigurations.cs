using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BARD.Infrastructure.Persistence.Configurations;

public class ExciseRateConfiguration : IEntityTypeConfiguration<ExciseRate>
{
    public void Configure(EntityTypeBuilder<ExciseRate> builder)
    {
        builder.ToTable("ExciseRates", schema: "reference");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ExciseCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(r => r.ExciseCode).IsUnique();
        builder.Property(r => r.Description).HasMaxLength(500).IsRequired();
        builder.Property(r => r.AdministrativeComment).HasMaxLength(1000);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasMany(r => r.Versions)
            .WithOne()
            .HasForeignKey(v => v.ExciseRateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class ExciseRateVersionConfiguration : IEntityTypeConfiguration<ExciseRateVersion>
{
    public void Configure(EntityTypeBuilder<ExciseRateVersion> builder)
    {
        builder.ToTable("ExciseRateVersions", schema: "reference");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Rate).HasColumnType("decimal(18,6)");
        builder.Property(v => v.CalculationUnit).HasConversion<string>().HasMaxLength(50);
        builder.HasIndex(v => new { v.ExciseRateId, v.EffectiveFrom });

        // Immutable once created — no update path is exposed anywhere in
        // the Application layer (historical reproducibility, decision #12).
    }
}

public class ExciseRateAuditEntryConfiguration : IEntityTypeConfiguration<ExciseRateAuditEntry>
{
    public void Configure(EntityTypeBuilder<ExciseRateAuditEntry> builder)
    {
        builder.ToTable("ExciseRateAuditEntries", schema: "audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.ExciseCode).HasMaxLength(20).IsRequired();
        builder.Property(a => a.PreviousRate).HasColumnType("decimal(18,6)");
        builder.Property(a => a.NewRate).HasColumnType("decimal(18,6)");
        builder.Property(a => a.PreviousUnit).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.NewUnit).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Reason).HasMaxLength(1000);
        builder.HasIndex(a => a.ExciseRateId);
    }
}
