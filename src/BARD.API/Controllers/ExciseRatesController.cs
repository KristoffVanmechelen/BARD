using BARD.Application.ExciseRates.Commands;
using BARD.Application.ExciseRates.Queries;
using BARD.Contracts.ExciseRates;
using BARD.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BARD.API.Controllers;

[ApiController]
[Route("api/v1/excise-rates")]
[Authorize]
public class ExciseRatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public ExciseRatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = PermissionCodes.ExciseRateView)]
    public async Task<ActionResult<IReadOnlyList<ExciseRateDto>>> GetAll([FromQuery] bool? activeOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetExciseRatesQuery(activeOnly), ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ExciseRateView)]
    public async Task<ActionResult<ExciseRateDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetExciseRateDetailQuery(id), ct));

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = PermissionCodes.ExciseRateViewHistory)]
    public async Task<ActionResult<IReadOnlyList<ExciseRateAuditEntryDto>>> GetHistory(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetExciseRateAuditHistoryQuery(id), ct));

    [HttpPost]
    [Authorize(Policy = PermissionCodes.ExciseRateEdit)]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateExciseRateRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateExciseRateCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:guid}/versions")]
    [Authorize(Policy = PermissionCodes.ExciseRateEdit)]
    public async Task<ActionResult<Guid>> PublishVersion(Guid id, [FromBody] PublishExciseRateVersionRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new PublishExciseRateVersionCommand(id, request), ct));

    [HttpPatch("{id:guid}/description")]
    [Authorize(Policy = PermissionCodes.ExciseRateEdit)]
    public async Task<IActionResult> UpdateDescription(Guid id, [FromBody] UpdateExciseRateDescriptionRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateExciseRateDescriptionCommand(id, request.Description), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PermissionCodes.ExciseRateEdit)]
    public async Task<IActionResult> Activate(Guid id, [FromBody] string? reason, CancellationToken ct)
    {
        await _mediator.Send(new SetExciseRateActiveStatusCommand(id, true, reason), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Policy = PermissionCodes.ExciseRateEdit)]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] string? reason, CancellationToken ct)
    {
        await _mediator.Send(new SetExciseRateActiveStatusCommand(id, false, reason), ct);
        return NoContent();
    }

    /// <summary>Enforces decision #11: deletion is rejected (409) if the rate was ever used in a dossier.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PermissionCodes.ExciseRateEdit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteExciseRateCommand(id), ct);
        return NoContent();
    }
}
