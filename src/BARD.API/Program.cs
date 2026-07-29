using BARD.API.DevAuth;
using BARD.API.Extensions;
using BARD.API.Middleware;
using BARD.Application;
using BARD.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging (Serilog per enterprise observability standards) ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

// --- Layers ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Authentication ---
// Production architecture is, and remains, Microsoft Entra ID.
//
// DevelopmentIdentityEnabled is ONLY ever true when a developer has
// explicitly set Development:SeedTestIdentity in a non-production
// config file (appsettings.Development.json). appsettings.Production.json
// hardcodes this to false, so this branch is structurally unreachable
// in a production deployment — Entra ID's JwtBearer scheme is always
// the (only) default there.
var developmentIdentityEnabled = builder.Configuration.GetValue<bool>("Development:SeedTestIdentity");

if (developmentIdentityEnabled)
{
    builder.Services
        .AddAuthentication(DevAuthenticationHandler.SchemeName)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevAuthenticationHandler>(
            DevAuthenticationHandler.SchemeName, _ => { });
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddAuthorization();
builder.Services.AddPermissionPolicies();

// --- CORS: allow the React SPA origin(s), configured per environment ---
// In Codespaces/dev-identity mode, the frontend's forwarded URL is a
// dynamic per-session *.app.github.dev host that can't be listed
// statically in appsettings — a origin-pattern match is used instead,
// scoped to that one gated dev mode. Production keeps the strict,
// statically-configured allow-list.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("SpaClient", policy =>
    {
        if (developmentIdentityEnabled)
        {
            policy.SetIsOriginAllowed(origin =>
                    origin.Contains("localhost") || origin.Contains(".app.github.dev") || origin.Contains(".githubpreview.dev"))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "BARD API",
        Version = "v1",
        Description = "Belgian Accise Refund & Document Analyzer — enterprise API.",
    });
});

var app = builder.Build();

// Idempotent RBAC + localization seeding (never overwrites administrator
// overrides; development test identity is gated by config and never
// runs unless explicitly enabled — see DatabaseSeeder for details).
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<BARD.Infrastructure.Persistence.Seed.DatabaseSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (!developmentIdentityEnabled)
{
    // In Codespaces/dev-identity mode, Kestrel runs HTTP-only and
    // Codespaces' own port forwarding terminates TLS — forcing a
    // redirect here would break the forwarded URL. Production and any
    // real-Entra-ID environment keep the redirect.
    app.UseHttpsRedirection();
}
app.UseCors("SpaClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
