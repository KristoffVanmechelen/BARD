using BARD.Application.Common.Interfaces;
using BARD.Application.Common.Options;
using BARD.Application.DocumentProcessing.Interfaces;
using BARD.Infrastructure.DocumentProcessing;
using BARD.Infrastructure.Persistence;
using BARD.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BARD.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BardDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("BardDatabase"),
                sql => sql.MigrationsAssembly(typeof(BardDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<BardDbContext>());

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        // --- Document processing pipeline (ported from the Python prototype) ---
        services.Configure<MatchThresholdsOptions>(configuration.GetSection(MatchThresholdsOptions.SectionName));
        services.Configure<ScoringWeightsOptions>(configuration.GetSection(ScoringWeightsOptions.SectionName));
        services.Configure<BusinessRulesOptions>(configuration.GetSection(BusinessRulesOptions.SectionName));
        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.Configure<AiAssistOptions>(configuration.GetSection(AiAssistOptions.SectionName));

        services.AddScoped<IPdfTextExtractionService, PdfPigTextExtractionService>();
        services.AddScoped<IOcrDetectionService, OcrDetectionService>();
        services.AddScoped<IOcrService, TesseractOcrService>();
        services.AddScoped<IDocumentClassifierService, DocumentClassifierService>();
        services.AddScoped<IInvoiceParsingService, InvoiceParsingService>();
        services.AddScoped<IAc4ParsingService, Ac4ParsingService>();
        services.AddScoped<IExcelClaimReaderService, ExcelClaimReaderService>();
        services.AddSingleton<IAliasResolverService, AliasResolverService>();
        services.AddScoped<IMatchingService, MatchingService>();
        services.AddScoped<IExportValidationService, ExportValidationService>();
        services.AddScoped<IMrnValidationService, MrnValidationService>();
        services.AddScoped<IRefundDeadlineValidationService, RefundDeadlineValidationService>();
        services.AddScoped<IRefundCalculationService, RefundCalculationService>();
        services.AddScoped<BARD.Application.AiAssist.IAiAssistService, BARD.Infrastructure.AiAssist.AzureOpenAiAssistService>();
        services.AddScoped<BARD.Application.Reporting.IDossierExportService, BARD.Infrastructure.Reporting.DossierExportService>();
        services.AddScoped<Persistence.Seed.DatabaseSeeder>();

        return services;
    }
}
