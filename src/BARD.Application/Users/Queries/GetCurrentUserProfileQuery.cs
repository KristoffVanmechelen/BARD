using BARD.Application.Common.Interfaces;
using BARD.Contracts.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BARD.Application.Users.Queries;

/// <summary>
/// Backs the frontend's permission-gating (audit finding H3): the SPA
/// needs to know which actions the current user may attempt so it can
/// hide/disable UI rather than only failing after submission. Server-side
/// [Authorize(Policy=...)] enforcement is unaffected and remains the
/// actual security boundary — this query is a UX convenience only.
/// </summary>
public record GetCurrentUserProfileQuery : IRequest<CurrentUserProfileDto>;

public class GetCurrentUserProfileQueryHandler : IRequestHandler<GetCurrentUserProfileQuery, CurrentUserProfileDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCurrentUserProfileQueryHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<CurrentUserProfileDto> Handle(GetCurrentUserProfileQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;

        var roleIds = await _db.UserRoleAssignments
            .Where(a => a.UserId == userId)
            .Select(a => a.RoleId)
            .ToListAsync(ct);

        var permissionCodes = await (
            from role in _db.Roles
            where roleIds.Contains(role.Id)
            join rp in _db.RolePermissions on role.Id equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            select p.Code
        ).Distinct().ToListAsync(ct);

        return new CurrentUserProfileDto(userId, _currentUser.DisplayName, _currentUser.PreferredLanguage, permissionCodes);
    }
}
