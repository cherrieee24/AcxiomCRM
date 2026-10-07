using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

[Route("api/customers")]
[Authorize]
public class CustomersApiController : CrmApiController
{
    private readonly CustomerService _customers;

    public CustomersApiController(CustomerService customers)
    {
        _customers = customers;
    }

    /// <summary>List/search customers visible to the caller. Query: search, status, assignedTo, sort, desc, page, pageSize.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] CustomerFilter filter) =>
        Ok(Page(await _customers.SearchAsync(filter), CustomerDto.From));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Get(int id)
    {
        var customer = await _customers.GetAsync(id);
        return customer == null ? NotFound() : CustomerDto.From(customer);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(CustomerInput input)
    {
        var result = await _customers.CreateAsync(input);
        if (!result.Succeeded) return Failure(result);
        var created = await _customers.GetAsync(result.Value!.CustomerId);
        return CreatedAtAction(nameof(Get), new { id = created!.CustomerId }, CustomerDto.From(created));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Update(int id, CustomerInput input)
    {
        var result = await _customers.UpdateAsync(id, input);
        if (!result.Succeeded) return Failure(result);
        return CustomerDto.From((await _customers.GetAsync(id))!);
    }

    /// <summary>Soft-deletes (deactivates) a customer. Admin/Manager only.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _customers.DeleteAsync(id);
        return result.Succeeded ? NoContent() : Failure(result);
    }
}
