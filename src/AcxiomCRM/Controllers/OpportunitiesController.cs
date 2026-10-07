using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class OpportunitiesController : CrmController
{
    private readonly OpportunityService _opportunities;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public OpportunitiesController(OpportunityService opportunities, CustomerService customers, LeadService leads,
        ICurrentUserService currentUser, IAuditService audit)
    {
        _opportunities = opportunities;
        _customers = customers;
        _leads = leads;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] OpportunityFilter filter)
    {
        return View(new ListViewModel<Opportunity, OpportunityFilter>
        {
            Items = await _opportunities.SearchAsync(filter),
            Filter = filter,
            Users = _currentUser.IsSalesExecutive ? new() : await _currentUser.GetAssignableUsersAsync(filter.AssignedTo)
        });
    }

    /// <summary>Kanban-style view of open opportunities by stage.</summary>
    public async Task<IActionResult> Pipeline()
    {
        var all = await _opportunities.SearchAsync(new OpportunityFilter { PageSize = 100, Sort = "close" });
        return View(all.Items);
    }

    public async Task<IActionResult> Details(int id)
    {
        var opp = await _opportunities.GetAsync(id);
        if (opp == null) return NotFound();
        return View(new DetailsViewModel<Opportunity> { Item = opp, History = await _audit.GetHistoryAsync(nameof(Opportunity), id) });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId)
    {
        var input = new OpportunityInput
        {
            CustomerId = customerId,
            Probability = 20,
            ExpectedCloseDate = DateTime.Today.AddDays(30),
            AssignedTo = _currentUser.IsSalesExecutive ? null : _currentUser.UserId
        };
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OpportunityInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _opportunities.CreateAsync(input);
            if (result.Succeeded)
            {
                Success("Opportunity created.");
                return RedirectToAction(nameof(Details), new { id = result.Value!.OpportunityId });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var opp = await _opportunities.GetAsync(id);
        if (opp == null) return NotFound();
        var input = OpportunityInput.From(opp);
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OpportunityInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _opportunities.UpdateAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Opportunity updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpGet]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var opp = await _opportunities.GetAsync(id);
        return opp == null ? NotFound() : View(opp);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _opportunities.DeleteAsync(id);
        return RedirectWithResult(result, "Opportunity deleted.", nameof(Index));
    }

    private async Task LoadLookupsAsync(OpportunityInput input)
    {
        ViewData["Customers"] = await _customers.GetOptionsAsync(input.CustomerId);
        ViewData["Leads"] = await _leads.GetOptionsAsync(input.LeadId);
        ViewData["Users"] = _currentUser.IsSalesExecutive ? null : await _currentUser.GetAssignableUsersAsync(input.AssignedTo);
    }
}
