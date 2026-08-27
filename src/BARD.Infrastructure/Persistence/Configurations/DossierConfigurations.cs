using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BARD.Infrastructure.Persistence.Configurations;

public class DossierConfiguration : IEntityTypeConfiguration<Dossier>
{
    public void Configure(EntityTypeBuilder<Dossier> builder)
    {
        builder.ToTable("Dossiers", schema: "dossier");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DossierReference)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(d => d.DossierReference)
            .IsUnique();

        builder.HasIndex(d => d.CompanyId);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.RowVersion)
            .IsRowVersion();

        builder.HasMany(d => d.Lines)
            .WithOne()
            .HasForeignKey(l => l.DossierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Documents)
            .WithOne()
            .HasForeignKey(doc => doc.DossierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.DossierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(d => d.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(d => d.Documents)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(d => d.StatusHistory)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(d => d.DomainEvents);
    }
}

public class DossierLineConfiguration
    : IEntityTypeConfiguration<DossierLine>
{
    public void Configure(EntityTypeBuilder<DossierLine> builder)
    {
        builder.ToTable("DossierLines", schema: "dossier");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.ClaimedInvoiceNumber)
            .HasMaxLength(100);

        builder.Property(l => l.ExciseCode)
            .HasMaxLength(20);

        builder.Property(l => l.Mrn)
            .HasMaxLength(30);

        builder.Property(l => l.ClaimedDestinationCountry)
            .HasMaxLength(2);

        builder.Property(l => l.ClaimedQuantity)
            .HasColumnType("decimal(18,4)");

        builder.Property(l => l.ConfidenceScore)
            .HasColumnType("decimal(5,2)");

        builder.Property(l => l.AppliedRate)
            .HasColumnType("decimal(18,6)");

        builder.Property(l => l.CalculatedRefundAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.CalculationNotes)
            .HasColumnType("nvarchar(max)");

        builder.Property(l => l.MatchStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.ExportStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.MrnCumulativeStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.Ac4Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.OfficerDecision)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(l => l.AppliedCalculationUnit)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(l => l.DossierId);
        builder.HasIndex(l => l.Mrn);
        builder.HasIndex(l => l.ClaimedInvoiceNumber);
    }
}

public class DossierDocumentConfiguration
    : IEntityTypeConfiguration<DossierDocument>
{
    public void Configure(EntityTypeBuilder<DossierDocument> builder)
    {
        builder.ToTable("DossierDocuments", schema: "dossier");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.OriginalFileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.BlobStoragePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(d => d.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.DocumentKind)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(d => d.DocumentRole)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(d => d.RoleConfidence)
            .HasColumnType("decimal(5,4)");

        builder.Property(d => d.RoleReasons)
            .HasColumnType("nvarchar(max)");

        builder.Property(d => d.ClassificationConfidence)
            .HasColumnType("decimal(5,4)");

        builder.Property(d => d.ClassificationReasons)
            .HasColumnType("nvarchar(max)");

        builder.Property(d => d.ExtractionMethod)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(d => d.ExtractionConfidence)
            .HasColumnType("decimal(5,4)");

        builder.Property(d => d.ExtractionWarnings)
            .HasColumnType("nvarchar(max)");

        builder.HasMany(d => d.ExtractedFields)
            .WithOne()
            .HasForeignKey(f => f.DossierDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.ExtractedFields)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(d => d.ContentHash);
        builder.HasIndex(d => d.DocumentKind);
        builder.HasIndex(d => d.DocumentRole);
    }
}

public class ExtractedFieldConfiguration
    : IEntityTypeConfiguration<ExtractedField>
{
    public void Configure(EntityTypeBuilder<ExtractedField> builder)
    {
        builder.ToTable("ExtractedFields", schema: "dossier");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FieldName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Confidence)
            .HasColumnType("decimal(5,4)");
    }
}

public class DossierStatusHistoryEntryConfiguration
    : IEntityTypeConfiguration<DossierStatusHistoryEntry>
{
    public void Configure(
        EntityTypeBuilder<DossierStatusHistoryEntry> builder)
    {
        builder.ToTable("DossierStatusHistory", schema: "dossier");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(h => h.Reason)
            .HasMaxLength(1000);
    }
}