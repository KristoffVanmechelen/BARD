using BARD.Application.Common.Exceptions;
using BARD.Application.Common.Interfaces;
using BARD.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace BARD.API.Middleware;

/// <summary>
/// Translates unhandled exceptions to RFC 7807 ProblemDetails responses.
///
/// Localization (audit finding H4): for LocalizableException-derived
/// exceptions carrying a LocalizationKey, this attempts to resolve a
/// translated message via LocalizationEntry, in the current user's
/// preferred language, before falling back to the exception's English
/// Message. Resolution failures (e.g. auth not yet resolved, no local
/// user record) are swallowed and fall back silently — this middleware
/// must never itself crash while handling an error.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApplicationDbContext db, ICurrentUserService currentUser)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, db, currentUser);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, IApplicationDbContext db, ICurrentUserService currentUser)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title) = exception switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "One or more validation errors occurred."),
            NotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            ForbiddenAccessException => (HttpStatusCode.Forbidden, "Access to this resource is forbidden."),
            BusinessRuleViolationException => (HttpStatusCode.Conflict, "The request violates a business rule."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(exception, "{ExceptionType} handling {Method} {Path}", exception.GetType().Name, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = (int)statusCode;

        var detail = exception.Message;
        if (exception is LocalizableException localizable && localizable.LocalizationKey is not null)
        {
            var resolved = await TryResolveLocalizedMessage(db, currentUser, localizable.LocalizationKey, localizable.Args);
            if (resolved is not null) detail = resolved;
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
        };

        if (exception is ValidationException validationException)
            problemDetails.Extensions["errors"] = validationException.Errors;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }

    private async Task<string?> TryResolveLocalizedMessage(IApplicationDbContext db, ICurrentUserService currentUser,
        string localizationKey, IReadOnlyDictionary<string, string>? args)
    {
        try
        {
            var entry = await db.LocalizationEntries
                .Include(e => e.Overrides)
                .FirstOrDefaultAsync(e => e.Key == localizationKey);
            if (entry is null) return null;

            var language = Enum.TryParse<UiLanguage>(currentUser.PreferredLanguage.Replace("-", ""), true, out var parsed)
                ? parsed
                : UiLanguage.NlBe;

            var template = entry.Overrides.FirstOrDefault(o => o.Language == language)?.Value ?? entry.GetDefault(language);

            if (args is not null)
                foreach (var (key, value) in args)
                    template = template.Replace($"{{{key}}}", value);

            return template;
        }
        catch
        {
            // Never let translation resolution itself break error handling
            // (e.g. no authenticated user yet, no local user record).
            return null;
        }
    }
}
