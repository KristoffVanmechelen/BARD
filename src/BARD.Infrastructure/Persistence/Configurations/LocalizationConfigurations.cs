using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BARD.Infrastructure.Persistence.Configurations;

public class LocalizationEntryConfiguration : IEntityTypeConfiguration<LocalizationEntry>
{
    public void Configure(EntityTypeBuilder<LocalizationEntry> builder)
    {
        builder.ToTable("LocalizationEntries", schema: "i18n");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();

        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Module).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Screen).HasMaxLength(100);

        builder.Property(e => e.DefaultNl).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.DefaultFr).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.DefaultDe).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.DefaultEn).HasMaxLength(2000).IsRequired();

        builder.HasMany(e => e.Overrides)
            .WithOne()
            .HasForeignKey(o => o.LocalizationEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Overrides).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => new { e.Module, e.Screen });
    }
}

public class TerminologyOverrideConfiguration : IEntityTypeConfiguration<TerminologyOverride>
{
    public void Configure(EntityTypeBuilder<TerminologyOverride> builder)
    {
        builder.ToTable("TerminologyOverrides", schema: "i18n");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Language).HasConversion<string>().HasMaxLength(10);
        builder.Property(o => o.Value).HasMaxLength(2000).IsRequired();

        builder.HasIndex(o => new { o.LocalizationEntryId, o.Language }).IsUnique();
    }
}

public class TerminologyAuditEntryConfiguration : IEntityTypeConfiguration<TerminologyAuditEntry>
{
    public void Configure(EntityTypeBuilder<TerminologyAuditEntry> builder)
    {
        builder.ToTable("TerminologyAuditEntries", schema: "audit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.LocalizationKey).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Language).HasConversion<string>().HasMaxLength(10);
        builder.Property(a => a.PreviousValue).HasMaxLength(2000);
        builder.Property(a => a.NewValue).HasMaxLength(2000);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(a => a.LocalizationKey);
    }
}
