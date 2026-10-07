using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/followups")]
[Authorize]
public class FollowUpsApiController : CrmApiController
{
    private readonly FollowUpService _followUps;

    public FollowUpsApiController(FollowUpService followUps)
    {
        _followUps = followUps;
    }

    /// <summary>Query: view=upcoming|overdue, status, from, to, search, assignedTo, page, pageSize.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] FollowUpFilter filter) =>
        Ok(Page(await _followUps.SearchAsync(filter), FollowUpDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FollowUpDto>> Get(int id)
    {
        var followUp = await _followUps.GetAsync(id);
        return followUp == null ? NotFound() : FollowUpDto.From(followUp);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FollowUpDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FollowUpDto>> Create(FollowUpInput input)
    {
        var result = await _followUps.CreateAsync(input);
        if (!result.Succeeded) return Failure(result);
        var created = await _followUps.GetAsync(result.Value!.FollowUpId);
        return CreatedAtAction(nameof(Get), new { id = result.Value.FollowUpId }, FollowUpDto.From(created!));
    }

    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteRequest? request)
    {
        var result = await _followUps.CompleteAsync(id, request?.Outcome);
        return result.Succeeded ? NoContent() : Failure(result);
    }

    public record CompleteRequest(string? Outcome);
}

[Route("api/reports")]
[Authorize(Roles = Roles.AdminOrManager)]
public class ReportsApiController : CrmApiController
{
    private readonly DashboardService _dashboard;

    public ReportsApiController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Stage-wise and owner-wise pipeline amounts (team-scoped for managers).</summary>
    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline() => Ok(await _dashboard.GetPipelineReportAsync());
}
