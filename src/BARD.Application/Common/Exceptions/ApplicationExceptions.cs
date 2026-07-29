using FluentValidation.Results;

namespace BARD.Application.Common.Exceptions;

/// <summary>
/// Base for exceptions whose message may be shown directly to an
/// officer. Carries an optional stable LocalizationKey (+ named Args
/// for interpolation) so the API's exception middleware can resolve a
/// translated message via the same LocalizationEntry mechanism used
/// for the rest of the UI (audit finding H4). When LocalizationKey is
/// null, or no matching entry exists, the English Message is used as
/// the fallback — this NEVER breaks existing behaviour, it only adds
/// an optional translation layer on top of it.
/// </summary>
public abstract class LocalizableException : Exception
{
    public string? LocalizationKey { get; }
    public IReadOnlyDictionary<string, string>? Args { get; }

    protected LocalizableException(string message, string? localizationKey = null,
        IReadOnlyDictionary<string, string>? args = null) : base(message)
    {
        LocalizationKey = localizationKey;
        Args = args;
    }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation failures have occurred.") =>
        Errors = new Dictionary<string, string[]>();

    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}

public class NotFoundException : LocalizableException
{
    public NotFoundException(string entityName, object key)
        : base($"Entity \"{entityName}\" ({key}) was not found.",
            "errors.not_found", new Dictionary<string, string> { ["entity"] = entityName, ["key"] = key.ToString() ?? "" })
    { }
}

public class ForbiddenAccessException : LocalizableException
{
    public ForbiddenAccessException(string requiredPermission)
        : base($"Access denied. Required permission: {requiredPermission}",
            "errors.forbidden", new Dictionary<string, string> { ["permission"] = requiredPermission })
    { }
}

public class BusinessRuleViolationException : LocalizableException
{
    public BusinessRuleViolationException(string message, string? localizationKey = null,
        IReadOnlyDictionary<string, string>? args = null) : base(message, localizationKey, args)
    { }
}
