using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class CustomersController : CrmController
{
    private readonly CustomerService _customers;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;

    public CustomersController(CustomerService customers, ICurrentUserService currentUser, IAuditService audit)
    {
        _customers = customers;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] CustomerFilter filter)
    {
        return View(new ListViewModel<Customer, CustomerFilter>
        {
            Items = await _customers.SearchAsync(filter),
            Filter = filter,
            Users = _currentUser.IsSalesExecutive ? new() : await _currentUser.GetAssignableUsersAsync(filter.AssignedTo)
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _customers.GetDetailsAsync(id);
        if (customer == null) return NotFound();
        return View(new DetailsViewModel<Customer>
        {
            Item = customer,
            History = await _audit.GetHistoryAsync(nameof(Customer), id)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var input = new CustomerInput { AssignedTo = _currentUser.IsSalesExecutive ? null : _currentUser.UserId };
        await LoadLookupsAsync(input.AssignedTo);
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _customers.CreateAsync(input);
            if (result.Succeeded)
            {
                Success($"Customer {result.Value!.CustomerCode} created.");
                return RedirectToAction(nameof(Details), new { id = result.Value.CustomerId });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input.AssignedTo);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _customers.GetAsync(id);
        if (customer == null) return NotFound();
        ViewData["Code"] = customer.CustomerCode;
        await LoadLookupsAsync(customer.AssignedTo);
        return View(CustomerInput.From(customer));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerInput input)
    {
        if (ModelState.IsValid)
        {
            var result = await _customers.UpdateAsync(id, input);
            if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
            if (result.Succeeded)
            {
                Success("Customer updated.");
                return RedirectToAction(nameof(Details), new { id });
            }
            AddErrors(result);
        }
        await LoadLookupsAsync(input.AssignedTo);
        return View(input);
    }

    [HttpGet]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _customers.GetAsync(id);
        return customer == null ? NotFound() : View(customer);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var result = await _customers.DeleteAsync(id);
        if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
        if (!result.Succeeded)
        {
            Error(string.Join(" ", result.Errors.Values));
            return RedirectToAction(nameof(Details), new { id });
        }
        Success("Customer deleted.");
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLookupsAsync(string? assignedTo)
    {
        ViewData["Users"] = _currentUser.IsSalesExecutive ? null : await _currentUser.GetAssignableUsersAsync(assignedTo);
    }
}
