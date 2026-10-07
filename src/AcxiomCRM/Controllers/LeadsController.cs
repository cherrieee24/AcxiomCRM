using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class LeadsController : CrmController
{
    private readonly LeadService _leads;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public LeadsController(LeadService leads, ICurrentUserService currentUser, IAuditService audit)
    {
        _leads = leads;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] LeadFilter filter)
    {
        return View(new ListViewModel<Lead, LeadFilter>
        {
            Items = await _leads.SearchAsync(filter),
            Filter = filter,
            Users = _currentUser.IsSalesExecutive ? new() : await _currentUser.GetAssignableUsersAsync(filter.AssignedTo)
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var lead = await _leads.GetAsync(id);
        if (lead == null) return NotFound();
        return View(new DetailsViewModel<Lead> { Item = lead, History = await _audit.GetHistoryAsync(nameof(Lead), id) });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var input = new LeadInput { AssignedTo = _currentUser.IsSalesExecutive ? null : _currentUser.UserId };
        await LoadLookupsAsync(input.AssignedTo, null);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeadInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _leads.CreateAsync(input);
            if (result.Succeeded)
            {
                Success($"Lead {result.Value!.LeadCode} created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.LeadId });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input.AssignedTo, null);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var lead = await _leads.GetAsync(id);
        if (lead == null) return NotFound();
        if (lead.Status == LeadStatus.Converted)
        {
            Error("Converted leads are read-only.");
            return RedirectToAction(nameof(Details), new { id });
        }
        ViewData["Code"] = lead.LeadCode;
        await LoadLookupsAsync(lead.AssignedTo, lead.Status);
        return View(LeadInput.From(lead));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, LeadInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _leads.UpdateAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Lead updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        var current = (await _leads.GetAsync(id))?.Status;
        await LoadLookupsAsync(input.AssignedTo, current);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Convert(int id)
    {
        var lead = await _leads.GetAsync(id);
        if (lead == null) return NotFound();
        if (lead.Status != LeadStatus.Qualified)
        {
            Error("Only Qualified leads can be converted.");
            return RedirectToAction(nameof(Details), new { id });
        }
        ViewData["Lead"] = lead;
        return View(new LeadConvertInput
        {
            OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - New Business",
            Amount = lead.ExpectedValue > 0 ? lead.ExpectedValue : null,
            Probability = 20,
            ExpectedCloseDate = DateTime.Today.AddDays(30)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Convert(int id, LeadConvertInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _leads.ConvertAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Lead converted successfully.");
                return RedirectToAction("Details", "Customers", new { id = result.Value!.ConvertedCustomerId });
            }
            AddErrors(result);
        }
        var lead = await _leads.GetAsync(id);
        if (lead == null) return NotFound();
        ViewData["Lead"] = lead;
        return View(input);
    }

    [HttpGet]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var lead = await _leads.GetAsync(id);
        return lead == null ? NotFound() : View(lead);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _leads.DeleteAsync(id);
        if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors.Values));
            return RedirectToAction(nameof(Details), new { id });
        }
        Success("Lead deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLookupsAsync(string? assignedTo, LeadStatus? currentStatus)
    {
        ViewData["Users"] = _currentUser.IsSalesExecutive ? null : await _currentUser.GetAssignableUsersAsync(assignedTo);
        ViewData["Statuses"] = LeadService.AllowedStatuses(currentStatus).ToList();
    }
}
