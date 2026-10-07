using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class LeadService : CrmServiceBase
{
    /// <summary>
    /// Allowed manual status changes. "Converted" is reached only through <see cref="ConvertAsync"/>,
    /// and a converted lead is final.
    /// </summary>
    public static readonly IReadOnlyDictionary<LeadStatus, LeadStatus[]> Transitions = new Dictionary<LeadStatus, LeadStatus[]>
    {
        [LeadStatus.New] = new[] { LeadStatus.Contacted, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Contacted] = new[] { LeadStatus.Qualified, LeadStatus.Unqualified, LeadStatus.Lost },
        [LeadStatus.Qualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Unqualified] = new[] { LeadStatus.Contacted, LeadStatus.Lost },
        [LeadStatus.Lost] = new[] { LeadStatus.New, LeadStatus.Contacted },
        [LeadStatus.Converted] = Array.Empty<LeadStatus>()
    };

    public static readonly LeadStatus[] InitialStatuses =
        { LeadStatus.New, LeadStatus.Contacted, LeadStatus.Qualified, LeadStatus.Unqualified };

    private readonly CustomerService _customers;
    private readonly OpportunityService _opportunities;
    private readonly IAuditService _audit;

    public LeadService(ApplicationDbContext db, ICurrentUserService currentUser, CustomerService customers,
        OpportunityService opportunities, IAuditService audit) : base(db, currentUser)
    {
        _customers = customers;
        _opportunities = opportunities;
        _audit = audit;
    }

    /// <summary>Statuses a user may pick for a lead currently in <paramref name="current"/> (null = new lead).</summary>
    public static IEnumerable<LeadStatus> AllowedStatuses(LeadStatus? current) =>
        current == null ? InitialStatuses : new[] { current.Value }.Concat(Transitions[current.Value]);

    private async Task<IQueryable<Lead>> ScopedAsync() =>
        Db.Leads.VisibleTo(await CurrentUser.GetScopeAsync());

    public async Task<PagedList<Lead>> SearchAsync(LeadFilter f)
    {
        var q = (await ScopedAsync()).Include(l => l.AssignedUser).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(l => l.LeadName.Contains(s) || l.LeadCode.Contains(s)
                             || (l.CompanyName != null && l.CompanyName.Contains(s))
                             || (l.Email != null && l.Email.Contains(s)));
        }
        if (f.Status.HasValue) q = q.Where(l => l.Status == f.Status);
        if (f.Source.HasValue) q = q.Where(l => l.Source == f.Source);
        if (!string.IsNullOrEmpty(f.AssignedTo)) q = q.Where(l => l.AssignedTo == f.AssignedTo);

        q = (f.Sort, f.Desc) switch
        {
            ("name", false) => q.OrderBy(l => l.LeadName),
            ("name", true) => q.OrderByDescending(l => l.LeadName),
            ("value", false) => q.OrderBy(l => l.ExpectedValue),
            ("value", true) => q.OrderByDescending(l => l.ExpectedValue),
            ("created", false) => q.OrderBy(l => l.CreatedDate),
            _ => q.OrderByDescending(l => l.CreatedDate)
        };

        return await PagedList<Lead>.CreateAsync(q, f.Page, f.PageSize);
    }

    public async Task<Lead?> GetAsync(int id) =>
        await (await ScopedAsync())
            .Include(l => l.AssignedUser)
            .Include(l => l.ConvertedCustomer)
            .Include(l => l.FollowUps)
            .Include(l => l.Activities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.LeadId == id);

    public async Task<List<SelectListItem>> GetOptionsAsync(int? selected = null) =>
        await (await ScopedAsync())
            .Where(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost || l.LeadId == selected)
            .OrderBy(l => l.LeadName)
            .Select(l => new SelectListItem(l.LeadName + " (" + l.LeadCode + ")", l.LeadId.ToString(), l.LeadId == selected))
            .ToListAsync();

    public async Task<ServiceResult<Lead>> CreateAsync(LeadInput input)
    {
        Normalize(input);
        var check = ValidateInput(input);
        if (check.Succeeded && !InitialStatuses.Contains(input.Status!.Value))
            check.AddError(nameof(input.Status), "A new lead cannot start as Converted or Lost.");

        var (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
        foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        if (!check.Succeeded) return ServiceResult<Lead>.From(check);

        var lead = new Lead { LeadCode = await NextCodeAsync(), CreatedBy = CurrentUser.UserId };
        Apply(lead, input, assignee);
        Db.Leads.Add(lead);
        await Db.SaveChangesAsync();
        return ServiceResult<Lead>.Ok(lead);
    }

    public async Task<ServiceResult<Lead>> UpdateAsync(int id, LeadInput input)
    {
        var lead = await (await ScopedAsync()).FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return ServiceResult<Lead>.From(ServiceResult.NotFound());

        Normalize(input);
        var check = ValidateInput(input);
        if (lead.Status == LeadStatus.Converted)
            check.AddError(string.Empty, "Converted leads are read-only.");
        else if (check.Succeeded && input.Status != lead.Status && !Transitions[lead.Status].Contains(input.Status!.Value))
            check.AddError(nameof(input.Status), $"Lead status cannot change from {lead.Status} to {input.Status}.");

        var assignee = lead.AssignedTo;
        if (!CurrentUser.IsSalesExecutive)
        {
            ServiceResult assignResult;
            (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
            foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        }
        if (!check.Succeeded) return ServiceResult<Lead>.From(check);

        Apply(lead, input, assignee);
        await Db.SaveChangesAsync();
        return ServiceResult<Lead>.Ok(lead);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var lead = await (await ScopedAsync()).FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return ServiceResult.NotFound();
        if (lead.Status == LeadStatus.Converted)
            return ServiceResult.Conflict(string.Empty, "Converted leads are kept for conversion reporting and cannot be deleted.");

        lead.IsDeleted = true;
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>
    /// Lead-to-Customer workflow (spec §8): a Qualified lead becomes a customer (re-using an existing
    /// customer with the same email) and optionally an opportunity, inside one transaction.
    /// </summary>
    public async Task<ServiceResult<Lead>> ConvertAsync(int id, LeadConvertInput input)
    {
        var lead = await (await ScopedAsync()).FirstOrDefaultAsync(l => l.LeadId == id);
        if (lead == null) return ServiceResult<Lead>.From(ServiceResult.NotFound());

        if (lead.Status != LeadStatus.Qualified)
            return ServiceResult<Lead>.From(ServiceResult.Invalid(string.Empty, "Only Qualified leads can be converted."));
        if (string.IsNullOrEmpty(lead.Email) || string.IsNullOrEmpty(lead.Phone))
            return ServiceResult<Lead>.From(ServiceResult.Invalid(string.Empty,
                "Add an email and phone number to the lead before converting it - customers require both."));

        var check = ValidateInput(input);
        if (!check.Succeeded) return ServiceResult<Lead>.From(check);

        await using var tx = await Db.Database.BeginTransactionAsync();
        try
        {
            var existing = await Db.Customers.FirstOrDefaultAsync(c => c.Email == lead.Email);
            Customer customer;
            if (existing != null)
            {
                var scope = await CurrentUser.GetScopeAsync();
                if (!scope.CanAccess(existing.AssignedTo))
                    return Fail(ServiceResult.Conflict(string.Empty, "A customer with this email exists and belongs to another team."));
                customer = existing;
            }
            else
            {
                var created = await _customers.CreateAsync(new CustomerInput
                {
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = CustomerStatus.Active,
                    Notes = lead.Notes,
                    AssignedTo = lead.AssignedTo
                });
                if (!created.Succeeded) return Fail(created);
                customer = created.Value!;
            }

            int? opportunityId = null;
            if (input.CreateOpportunity)
            {
                var opp = await _opportunities.CreateAsync(new OpportunityInput
                {
                    OpportunityName = Clean(input.OpportunityName) ?? $"{lead.CompanyName ?? lead.LeadName} - New Business",
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = input.Amount ?? lead.ExpectedValue,
                    Stage = OpportunityStage.Qualification,
                    Probability = input.Probability ?? 20,
                    ExpectedCloseDate = input.ExpectedCloseDate ?? DateTime.Today.AddDays(30),
                    AssignedTo = lead.AssignedTo
                });
                if (!opp.Succeeded) return Fail(opp);
                opportunityId = opp.Value!.OpportunityId;
            }

            lead.Status = LeadStatus.Converted;
            lead.ConvertedDate = DateTime.UtcNow;
            lead.ConvertedCustomerId = customer.CustomerId;
            await Db.SaveChangesAsync();

            await _audit.LogAsync(AuditActions.Convert, nameof(Lead), lead.LeadId.ToString(),
                new { lead.LeadCode, CustomerId = customer.CustomerId, OpportunityId = opportunityId });

            await tx.CommitAsync();
            return ServiceResult<Lead>.Ok(lead);
        }
        catch
        {
            Db.ChangeTracker.Clear();
            throw;
        }

        ServiceResult<Lead> Fail(ServiceResult failure)
        {
            // Transaction is rolled back on dispose; forget the unsaved/rolled-back entities.
            Db.ChangeTracker.Clear();
            return ServiceResult<Lead>.From(failure);
        }
    }

    private static void Normalize(LeadInput input)
    {
        input.LeadName = input.LeadName?.Trim() ?? string.Empty;
        input.Email = Clean(input.Email)?.ToLowerInvariant();
        input.Phone = Clean(input.Phone);
        input.CompanyName = Clean(input.CompanyName);
    }

    private static void Apply(Lead l, LeadInput input, string? assignee)
    {
        l.LeadName = input.LeadName;
        l.Email = input.Email;
        l.Phone = input.Phone;
        l.CompanyName = input.CompanyName;
        l.Source = input.Source!.Value;
        l.Status = input.Status!.Value;
        l.Priority = input.Priority ?? Priority.Medium;
        l.ExpectedValue = input.ExpectedValue ?? 0;
        l.Notes = Clean(input.Notes);
        l.AssignedTo = assignee;
    }

    private async Task<string> NextCodeAsync()
    {
        var count = await Db.Leads.IgnoreQueryFilters().CountAsync();
        return $"LD-{count + 1:D5}";
    }
}
