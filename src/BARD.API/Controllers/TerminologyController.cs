using BARD.Application.Terminology.Commands;
using BARD.Application.Terminology.Queries;
using BARD.Contracts.Terminology;
using BARD.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BARD.API.Controllers;

[ApiController]
[Route("api/v1/terminology")]
[Authorize]
public class TerminologyController : ControllerBase
{
    private readonly IMediator _mediator;

    public TerminologyController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Returns the flat translation bundle for one language. Called by
    /// the frontend's i18next backend on app start / language switch.
    /// Deliberately available to any authenticated user (not gated by
    /// the terminology-management permissions) since every user needs
    /// their UI text, not just administrators.
    /// </summary>
    [HttpGet("bundle/{language}")]
    public async Task<ActionResult<LocalizationBundleDto>> GetBundle(string language, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLocalizationBundleQuery(language), ct));

    [HttpPost("search")]
    [Authorize(Policy = PermissionCodes.TerminologyView)]
    public async Task<ActionResult<TerminologySearchResultDto>> Search([FromBody] TerminologySearchRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new SearchTerminologyQuery(request), ct));

    [HttpGet("{key}/history")]
    [Authorize(Policy = PermissionCodes.TerminologyViewHistory)]
    public async Task<ActionResult<IReadOnlyList<TerminologyHistoryEntryDto>>> GetHistory(string key, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTerminologyHistoryQuery(key), ct));

    /// <summary>
    /// Single write path shared by the central Settings > Terminology
    /// page and the inline editor — the Source field on the request
    /// records which one was used, but both hit this same endpoint, so
    /// the "changes appear in both places" guarantee (decision #7) is
    /// structural rather than something each caller must remember.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateTerminologyRequest request, CancellationToken ct)
    {
        var requiredPolicy = request.Source == "InlineEditor"
            ? PermissionCodes.TerminologyInlineEdit
            : PermissionCodes.TerminologyEditCentral;

        var authResult = await HttpContext.RequestServices
            .GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(User, requiredPolicy);

        if (!authResult.Succeeded) return Forbid();

        await _mediator.Send(new UpdateTerminologyCommand(request), ct);
        return NoContent();
    }

    [HttpPost("restore-default")]
    [Authorize(Policy = PermissionCodes.TerminologyRestoreDefault)]
    public async Task<IActionResult> RestoreDefault([FromBody] RestoreTerminologyDefaultRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RestoreTerminologyDefaultCommand(request), ct);
        return NoContent();
    }
}
