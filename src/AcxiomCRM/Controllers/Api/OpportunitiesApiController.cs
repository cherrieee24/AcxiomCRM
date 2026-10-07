using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/opportunities")]
[Authorize]
public class OpportunitiesApiController : CrmApiController
{
    private readonly OpportunityService _opportunities;

    public OpportunitiesApiController(OpportunityService opportunities)
    {
        _opportunities = opportunities;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] OpportunityFilter filter) =>
        Ok(Page(await _opportunities.SearchAsync(filter), OpportunityDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OpportunityDto>> Get(int id)
    {
        var opp = await _opportunities.GetAsync(id);
        return opp == null ? NotFound() : OpportunityDto.From(opp);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OpportunityDto>> Create(OpportunityInput input)
    {
        var result = await _opportunities.CreateAsync(input);
        if (!result.Succeeded) return Failure(result);
        var created = await _opportunities.GetAsync(result.Value!.OpportunityId);
        return CreatedAtAction(nameof(Get), new { id = result.Value.OpportunityId }, OpportunityDto.From(created!));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OpportunityDto>> Update(int id, OpportunityInput input)
    {
        var result = await _opportunities.UpdateAsync(id, input);
        if (!result.Succeeded) return Failure(result);
        return OpportunityDto.From((await _opportunities.GetAsync(id))!);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _opportunities.DeleteAsync(id);
        return result.Succeeded ? NoContent() : Failure(result);
    }
}
