using BARD.Application.Common.Interfaces;
using BARD.Domain.Entities;
using BARD.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace BARD.Infrastructure.Services;

/// <summary>
/// Resolves the current user's identity and permissions from Entra ID
/// claims on the HTTP context, per the enterprise RBAC model.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly BardDbContext _db;
    private HashSet<string>? _cachedPermissions;
    private User? _cachedUser;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, BardDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    private User ResolveUser()
    {
        if (_cachedUser is not null) return _cachedUser;

        var externalId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("No authenticated user on the current request.");

        _cachedUser = _db.Users
            .Include(u => u.RoleAssignments)
            .FirstOrDefault(u => u.ExternalIdentityId == externalId)
            ?? throw new InvalidOperationException($"No local user record for identity '{externalId}'. Provisioning required.");

        return _cachedUser;
    }

    public Guid UserId => ResolveUser().Id;
    public string DisplayName => ResolveUser().DisplayName;
    public string PreferredLanguage => ResolveUser().PreferredLanguage;

    public bool HasPermission(string permissionCode)
    {
        _cachedPermissions ??= LoadPermissions();
        return _cachedPermissions.Contains(permissionCode);
    }

    private HashSet<string> LoadPermissions()
    {
        var user = ResolveUser();
        var roleIds = user.RoleAssignments.Select(a => a.RoleId).ToList();

        var permissionCodes = (from role in _db.Roles
                                where roleIds.Contains(role.Id)
                                join rp in _db.RolePermissions on role.Id equals rp.RoleId
                                join p in _db.Permissions on rp.PermissionId equals p.Id
                                select p.Code).Distinct().ToList();

        return permissionCodes.ToHashSet();
    }
}

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}

public class AuditLogger : IAuditLogger
{
    private readonly BardDbContext _db;

    public AuditLogger(BardDbContext db) => _db = db;

    public async Task LogAsync(string entityType, Guid entityId, string action, object? details, CancellationToken ct = default)
    {
        var json = details is null ? null : JsonSerializer.Serialize(details);
        var entry = AuditLogEntry.Create(entityType, entityId, action, json, null);
        _db.AuditLogEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}
