using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class FollowUpsController : CrmController
{
    private readonly FollowUpService _followUps;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly OpportunityService _opportunities;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public FollowUpsController(FollowUpService followUps, CustomerService customers, LeadService leads,
        OpportunityService opportunities, ICurrentUserService currentUser, IAuditService audit)
    {
        _followUps = followUps;
        _customers = customers;
        _leads = leads;
        _opportunities = opportunities;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] FollowUpFilter filter)
    {
        return View(new ListViewModel<FollowUp, FollowUpFilter>
        {
            Items = await _followUps.SearchAsync(filter),
            Filter = filter,
            Users = _currentUser.IsSalesExecutive ? new() : await _currentUser.GetAssignableUsersAsync(filter.AssignedTo)
        });
    }

    /// <summary>Pending = every planned follow-up, grouped into overdue / today / this week / later.</summary>
    public async Task<IActionResult> Pending(string? assignedTo)
    {
        var planned = await _followUps.SearchAsync(new FollowUpFilter
        {
            Status = FollowUpStatus.Planned,
            AssignedTo = assignedTo,
            Sort = "date",
            PageSize = 100
        });
        ViewData["Users"] = _currentUser.IsSalesExecutive ? null : await _currentUser.GetAssignableUsersAsync(assignedTo);
        ViewData["AssignedTo"] = assignedTo;
        return View(planned);
    }

    public async Task<IActionResult> Details(int id)
    {
        var followUp = await _followUps.GetAsync(id);
        if (followUp == null) return NotFound();
        return View(new DetailsViewModel<FollowUp> { Item = followUp, History = await _audit.GetHistoryAsync(nameof(FollowUp), id) });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
    {
        var input = new FollowUpInput
        {
            CustomerId = customerId,
            LeadId = leadId,
            OpportunityId = opportunityId,
            FollowUpDate = DateTime.Today.AddDays(1),
            AssignedTo = _currentUser.IsSalesExecutive ? null : _currentUser.UserId
        };
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FollowUpInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _followUps.CreateAsync(input);
            if (result.Succeeded)
            {
                Success("Follow-up scheduled.");
                return RedirectToAction(nameof(Details), new { id = result.Value!.FollowUpId });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var followUp = await _followUps.GetAsync(id);
        if (followUp == null) return NotFound();
        if (followUp.Status != FollowUpStatus.Planned)
        {
            Error("Only planned follow-ups can be edited.");
            return RedirectToAction(nameof(Details), new { id });
        }
        var input = FollowUpInput.From(followUp);
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FollowUpInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _followUps.UpdateAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Follow-up updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? outcome, string? returnUrl)
    {
        var result = await _followUps.CompleteAsync(id, outcome);
        if (result.Succeeded && Url.IsLocalUrl(returnUrl))
        {
            Success("Follow-up marked as completed.");
            return LocalRedirect(returnUrl);
        }
        return RedirectWithResult(result, "Follow-up marked as completed.", nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Missed(int id) =>
        RedirectWithResult(await _followUps.MarkMissedAsync(id), "Follow-up marked as missed.", nameof(Details), new { id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id) =>
        RedirectWithResult(await _followUps.CancelAsync(id), "Follow-up cancelled.", nameof(Details), new { id });

    [HttpGet]
    public async Task<IActionResult> Reschedule(int id)
    {
        var followUp = await _followUps.GetAsync(id);
        if (followUp == null) return NotFound();
        ViewData["FollowUp"] = followUp;
        return View(new FollowUpRescheduleInput { FollowUpDate = DateTime.Today.AddDays(1) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(int id, FollowUpRescheduleInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _followUps.RescheduleAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Follow-up rescheduled.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        var followUp = await _followUps.GetAsync(id);
        if (followUp == null) return NotFound();
        ViewData["FollowUp"] = followUp;
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id) =>
        RedirectWithResult(await _followUps.DeleteAsync(id), "Follow-up deleted.", nameof(Index));

    private async Task LoadLookupsAsync(FollowUpInput input)
    {
        ViewData["Customers"] = await _customers.GetOptionsAsync(input.CustomerId);
        ViewData["Leads"] = await _leads.GetOptionsAsync(input.LeadId);
        ViewData["Opportunities"] = await _opportunities.GetOptionsAsync(input.OpportunityId);
        ViewData["Users"] = _currentUser.IsSalesExecutive ? null : await _currentUser.GetAssignableUsersAsync(input.AssignedTo);
    }
}
