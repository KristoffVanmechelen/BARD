using BARD.Domain.Common;

namespace BARD.Domain.Entities;

/// <summary>
/// The applicant company for a refund dossier (Phase 5, pulled forward
/// into the upload workflow per the approved execution plan — Phase 2
/// requires an identifiable applicant at upload time).
///
/// Identity is the normalized enterprise/VAT number (authoritative
/// decision #4): the upload form's explicitly supplied name + number
/// are authoritative; Excel-derived company information is never used
/// to create or match a Company record on its own.
/// </summary>
public class Company : AuditableEntity
{
    public string Name { get; private set; } = default!;

    /// <summary>Original, as-entered enterprise/VAT number (display value).</summary>
    public string EnterpriseNumber { get; private set; } = default!;

    /// <summary>Normalized form used for matching/deduplication — see CompanyNumberNormalizer.</summary>
    public string NormalizedEnterpriseNumber { get; private set; } = default!;

    public string? AddressLine { get; private set; }
    public string? PostalCode { get; private set; }
    public string? City { get; private set; }
    public string? Country { get; private set; }

    protected Company() { }

    public static Company Create(string name, string enterpriseNumber, string normalizedEnterpriseNumber,
        Guid createdByUserId, string? addressLine = null, string? postalCode = null, string? city = null, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Company name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(normalizedEnterpriseNumber))
            throw new ArgumentException("Enterprise/VAT number is required.", nameof(enterpriseNumber));

        return new Company
        {
            Id = Guid.NewGuid(),
            Name = name,
            EnterpriseNumber = enterpriseNumber,
            NormalizedEnterpriseNumber = normalizedEnterpriseNumber,
            AddressLine = addressLine,
            PostalCode = postalCode,
            City = city,
            Country = country,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = createdByUserId,
        };
    }

    /// <summary>Updates display fields on a re-matched company without changing its identity (the normalized number).</summary>
    public void UpdateContactDetails(string name, string? addressLine, string? postalCode, string? city, string? country, Guid changedByUserId)
    {
        Name = name;
        AddressLine = addressLine ?? AddressLine;
        PostalCode = postalCode ?? PostalCode;
        City = city ?? City;
        Country = country ?? Country;
        ModifiedAtUtc = DateTime.UtcNow;
        ModifiedByUserId = changedByUserId;
    }
}
