using BARD.Application.Users.Queries;
using BARD.Contracts.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BARD.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Current user's profile + resolved permission codes, for frontend UI gating (audit finding H3).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserProfileDto>> GetMe(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCurrentUserProfileQuery(), ct));
}
