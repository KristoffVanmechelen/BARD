using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BARD.Infrastructure.Persistence.Configurations;

public class Ac4DeclarationConfiguration : IEntityTypeConfiguration<Ac4Declaration>
{
    public void Configure(EntityTypeBuilder<Ac4Declaration> builder)
    {
        builder.ToTable("Ac4Declarations", schema: "dossier");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Mrn).HasMaxLength(30);
        builder.Property(a => a.Consignee).HasMaxLength(1000);
        builder.Property(a => a.ProductDescription).HasMaxLength(1000);
        builder.Property(a => a.ExciseCode).HasMaxLength(20);
        builder.Property(a => a.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(a => a.ExtractionMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.ExtractionConfidence).HasColumnType("decimal(5,4)");
        builder.Property(a => a.ExtractionWarnings).HasColumnType("nvarchar(max)");

        // Explicit FK to DossierDocument (audit finding L1 — was previously
        // an unenforced index only). No navigation collection is exposed
        // on DossierDocument, consistent with Ac4Declaration being an
        // independently-queried entity rather than part of the
        // DossierDocument aggregate's public shape.
        builder.HasOne<DossierDocument>()
            .WithMany()
            .HasForeignKey(a => a.DossierDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.DossierDocumentId);
        builder.HasIndex(a => a.Mrn);
    }
}

