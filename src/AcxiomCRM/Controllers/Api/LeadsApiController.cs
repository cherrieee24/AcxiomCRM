using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/leads")]
[Authorize]
public class LeadsApiController : CrmApiController
{
    private readonly LeadService _leads;

    public LeadsApiController(LeadService leads)
    {
        _leads = leads;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] LeadFilter filter) =>
        Ok(Page(await _leads.SearchAsync(filter), LeadDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> Get(int id)
    {
        var lead = await _leads.GetAsync(id);
        return lead == null ? NotFound() : LeadDto.From(lead);
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeadDto>> Create(LeadInput input)
    {
        var result = await _leads.CreateAsync(input);
        if (!result.Succeeded) return Failure(result);
        var created = await _leads.GetAsync(result.Value!.LeadId);
        return CreatedAtAction(nameof(Get), new { id = created!.LeadId }, LeadDto.From(created));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> Update(int id, LeadInput input)
    {
        var result = await _leads.UpdateAsync(id, input);
        if (!result.Succeeded) return Failure(result);
        return LeadDto.From((await _leads.GetAsync(id))!);
    }

    [HttpPost("{id:int}/convert")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> Convert(int id, LeadConvertInput input)
    {
        var result = await _leads.ConvertAsync(id, input);
        if (!result.Succeeded) return Failure(result);
        return LeadDto.From(result.Value!);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _leads.DeleteAsync(id);
        return result.Succeeded ? NoContent() : Failure(result);
    }
}
