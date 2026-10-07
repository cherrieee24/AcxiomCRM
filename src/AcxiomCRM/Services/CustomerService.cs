using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class CustomerService : CrmServiceBase
{
    public CustomerService(ApplicationDbContext db, ICurrentUserService currentUser) : base(db, currentUser) { }

    private async Task<IQueryable<Customer>> ScopedAsync() =>
        Db.Customers.VisibleTo(await CurrentUser.GetScopeAsync());

    public async Task<PagedList<Customer>> SearchAsync(CustomerFilter f)
    {
        var q = (await ScopedAsync()).Include(c => c.AssignedUser).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(c => c.CustomerName.Contains(s) || c.Email.Contains(s) || c.Phone.Contains(s)
                             || (c.CompanyName != null && c.CompanyName.Contains(s)) || c.CustomerCode.Contains(s));
        }
        if (f.Status.HasValue) q = q.Where(c => c.Status == f.Status);
        if (!string.IsNullOrEmpty(f.AssignedTo)) q = q.Where(c => c.AssignedTo == f.AssignedTo);

        q = (f.Sort, f.Desc) switch
        {
            ("name", false) => q.OrderBy(c => c.CustomerName),
            ("name", true) => q.OrderByDescending(c => c.CustomerName),
            ("company", false) => q.OrderBy(c => c.CompanyName),
            ("company", true) => q.OrderByDescending(c => c.CompanyName),
            ("created", false) => q.OrderBy(c => c.CreatedDate),
            _ => q.OrderByDescending(c => c.CreatedDate)
        };

        return await PagedList<Customer>.CreateAsync(q, f.Page, f.PageSize);
    }

    public async Task<Customer?> GetAsync(int id) =>
        await (await ScopedAsync()).Include(c => c.AssignedUser).FirstOrDefaultAsync(c => c.CustomerId == id);

    public async Task<Customer?> GetDetailsAsync(int id) =>
        await (await ScopedAsync())
            .Include(c => c.AssignedUser)
            .Include(c => c.Opportunities)
            .Include(c => c.FollowUps).ThenInclude(f => f.AssignedUser)
            .Include(c => c.Activities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.CustomerId == id);

    public async Task<List<SelectListItem>> GetOptionsAsync(int? selected = null) =>
        await (await ScopedAsync())
            .OrderBy(c => c.CustomerName)
            .Select(c => new SelectListItem(c.CustomerName + " (" + c.CustomerCode + ")", c.CustomerId.ToString(), c.CustomerId == selected))
            .ToListAsync();

    public async Task<ServiceResult<Customer>> CreateAsync(CustomerInput input)
    {
        Normalize(input);
        var check = await ValidateAsync(input, null);
        var (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
        foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        if (!check.Succeeded) return ServiceResult<Customer>.From(check);

        var customer = new Customer { CustomerCode = await NextCodeAsync(), CreatedBy = CurrentUser.UserId };
        Apply(customer, input, assignee);
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync();
        return ServiceResult<Customer>.Ok(customer);
    }

    public async Task<ServiceResult<Customer>> UpdateAsync(int id, CustomerInput input)
    {
        var customer = await (await ScopedAsync()).FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return ServiceResult<Customer>.From(ServiceResult.NotFound());

        Normalize(input);
        var check = await ValidateAsync(input, id);
        var assignee = customer.AssignedTo;
        if (!CurrentUser.IsSalesExecutive)
        {
            ServiceResult assignResult;
            (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
            foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        }
        if (!check.Succeeded) return ServiceResult<Customer>.From(check);

        Apply(customer, input, assignee);
        await Db.SaveChangesAsync();
        return ServiceResult<Customer>.Ok(customer);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var customer = await (await ScopedAsync()).FirstOrDefaultAsync(c => c.CustomerId == id);
        if (customer == null) return ServiceResult.NotFound();

        if (await Db.Opportunities.AnyAsync(o => o.CustomerId == id && o.Status == OpportunityStatus.Open))
            return ServiceResult.Conflict(string.Empty, "This customer has open opportunities. Close or reassign them before deleting.");

        customer.IsDeleted = true; // soft delete keeps history and audit trail intact
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Required/format rules plus uniqueness and duplicate-customer business rules (spec §17.5).</summary>
    private async Task<ServiceResult> ValidateAsync(CustomerInput input, int? existingId)
    {
        var result = ValidateInput(input);
        if (!result.Succeeded) return result;

        // Uniqueness is global (not scoped) - two reps must not create the same customer.
        if (await Db.Customers.AnyAsync(c => c.Email == input.Email && c.CustomerId != existingId))
            return ServiceResult.Conflict(nameof(input.Email), "A customer with this email already exists.");

        if (await Db.Customers.AnyAsync(c => c.Phone == input.Phone && c.CustomerId != existingId))
            return ServiceResult.Conflict(nameof(input.Phone), "A customer with this phone number already exists.");

        var name = input.CustomerName.ToLower();
        var company = input.CompanyName?.ToLower();
        if (await Db.Customers.AnyAsync(c => c.CustomerName.ToLower() == name
                                             && (c.CompanyName == null ? company == null : c.CompanyName.ToLower() == company)
                                             && c.CustomerId != existingId))
            return ServiceResult.Conflict(nameof(input.CustomerName), "This customer already exists for the same company.");

        return result;
    }

    private static void Normalize(CustomerInput input)
    {
        input.CustomerName = input.CustomerName?.Trim() ?? string.Empty;
        input.Email = input.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        input.Phone = input.Phone?.Trim() ?? string.Empty;
        input.CompanyName = Clean(input.CompanyName);
    }

    private static void Apply(Customer c, CustomerInput input, string? assignee)
    {
        c.CustomerName = input.CustomerName;
        c.Email = input.Email;
        c.Phone = input.Phone;
        c.CompanyName = input.CompanyName;
        c.Address = Clean(input.Address);
        c.City = Clean(input.City);
        c.State = Clean(input.State);
        c.Status = input.Status ?? CustomerStatus.Active;
        c.Notes = Clean(input.Notes);
        c.AssignedTo = assignee;
    }

    private async Task<string> NextCodeAsync()
    {
        var count = await Db.Customers.IgnoreQueryFilters().CountAsync();
        return $"CUS-{count + 1:D5}";
    }
}
