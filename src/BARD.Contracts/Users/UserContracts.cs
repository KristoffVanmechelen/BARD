namespace BARD.Contracts.Users;

public record CurrentUserProfileDto(
    Guid UserId,
    string DisplayName,
    string PreferredLanguage,
    IReadOnlyList<string> Permissions
);
