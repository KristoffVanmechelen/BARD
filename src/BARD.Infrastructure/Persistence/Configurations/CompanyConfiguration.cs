using BARD.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BARD.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies", schema: "dossier");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(500).IsRequired();
        builder.Property(c => c.EnterpriseNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.NormalizedEnterpriseNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.AddressLine).HasMaxLength(500);
        builder.Property(c => c.PostalCode).HasMaxLength(20);
        builder.Property(c => c.City).HasMaxLength(200);
        builder.Property(c => c.Country).HasMaxLength(2);
        builder.Property(c => c.RowVersion).IsRowVersion();

        // Deduplication key per authoritative decision #4.
        builder.HasIndex(c => c.NormalizedEnterpriseNumber).IsUnique();
    }
}
