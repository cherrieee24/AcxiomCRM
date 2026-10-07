using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class ActivitiesController : CrmController
{
    private readonly ActivityService _activities;
    private readonly CustomerService _customers;
    private readonly LeadService _leads;
    private readonly ICurrentUserService _currentUser;

    public ActivitiesController(ActivityService activities, CustomerService customers, LeadService leads, ICurrentUserService currentUser)
    {
        _activities = activities;
        _customers = customers;
        _leads = leads;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index([FromQuery] ActivityFilter filter)
    {
        return View(new ListViewModel<Activity, ActivityFilter>
        {
            Items = await _activities.SearchAsync(filter),
            Filter = filter,
            Users = _currentUser.IsSalesExecutive ? new() : await _currentUser.GetAssignableUsersAsync(filter.AssignedTo)
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var activity = await _activities.GetAsync(id);
        return activity == null ? NotFound() : View(activity);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? customerId, int? leadId, ActivityType? type)
    {
        var input = new ActivityInput
        {
            CustomerId = customerId,
            LeadId = leadId,
            ActivityType = type ?? ActivityType.Call,
            AssignedTo = _currentUser.IsSalesExecutive ? null : _currentUser.UserId
        };
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ActivityInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _activities.CreateAsync(input);
            if (result.Succeeded)
            {
                Success("Activity logged.");
                return RedirectToAction(nameof(Index));
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var activity = await _activities.GetAsync(id);
        if (activity == null) return NotFound();
        var input = ActivityInput.From(activity);
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ActivityInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _activities.UpdateAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Activity updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id) =>
        RedirectWithResult(await _activities.DeleteAsync(id), "Activity deleted.", nameof(Index));

    private async Task LoadLookupsAsync(ActivityInput input)
    {
        ViewData["Customers"] = await _customers.GetOptionsAsync(input.CustomerId);
        ViewData["Leads"] = await _leads.GetOptionsAsync(input.LeadId);
        ViewData["Users"] = _currentUser.IsSalesExecutive ? null : await _currentUser.GetAssignableUsersAsync(input.AssignedTo);
    }
}
