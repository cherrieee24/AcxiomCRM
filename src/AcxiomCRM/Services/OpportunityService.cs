using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class OpportunityService : CrmServiceBase
{
    public OpportunityService(ApplicationDbContext db, ICurrentUserService currentUser) : base(db, currentUser) { }

    private async Task<IQueryable<Opportunity>> ScopedAsync() =>
        Db.Opportunities.VisibleTo(await CurrentUser.GetScopeAsync());

    public async Task<PagedList<Opportunity>> SearchAsync(OpportunityFilter f)
    {
        var q = (await ScopedAsync()).Include(o => o.Customer).Include(o => o.AssignedUser).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(o => o.OpportunityName.Contains(s) || o.Customer!.CustomerName.Contains(s));
        }
        if (f.Stage.HasValue) q = q.Where(o => o.Stage == f.Stage);
        if (f.Status.HasValue) q = q.Where(o => o.Status == f.Status);
        if (f.CustomerId.HasValue) q = q.Where(o => o.CustomerId == f.CustomerId);
        if (!string.IsNullOrEmpty(f.AssignedTo)) q = q.Where(o => o.AssignedTo == f.AssignedTo);

        q = (f.Sort, f.Desc) switch
        {
            ("name", false) => q.OrderBy(o => o.OpportunityName),
            ("name", true) => q.OrderByDescending(o => o.OpportunityName),
            ("amount", false) => q.OrderBy(o => o.Amount),
            ("amount", true) => q.OrderByDescending(o => o.Amount),
            ("close", false) => q.OrderBy(o => o.ExpectedCloseDate),
            ("close", true) => q.OrderByDescending(o => o.ExpectedCloseDate),
            _ => q.OrderByDescending(o => o.CreatedDate)
        };

        return await PagedList<Opportunity>.CreateAsync(q, f.Page, f.PageSize);
    }

    public async Task<Opportunity?> GetAsync(int id) =>
        await (await ScopedAsync())
            .Include(o => o.Customer)
            .Include(o => o.Lead)
            .Include(o => o.AssignedUser)
            .Include(o => o.FollowUps)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.OpportunityId == id);

    public async Task<List<SelectListItem>> GetOptionsAsync(int? selected = null) =>
        await (await ScopedAsync())
            .Where(o => o.Status == OpportunityStatus.Open || o.OpportunityId == selected)
            .OrderBy(o => o.OpportunityName)
            .Select(o => new SelectListItem(o.OpportunityName, o.OpportunityId.ToString(), o.OpportunityId == selected))
            .ToListAsync();

    public async Task<ServiceResult<Opportunity>> CreateAsync(OpportunityInput input)
    {
        var check = await ValidateAsync(input, existing: null);
        var (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
        foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        if (!check.Succeeded) return ServiceResult<Opportunity>.From(check);

        var opp = new Opportunity { CreatedBy = CurrentUser.UserId };
        Apply(opp, input, assignee);
        Db.Opportunities.Add(opp);
        await Db.SaveChangesAsync();
        return ServiceResult<Opportunity>.Ok(opp);
    }

    public async Task<ServiceResult<Opportunity>> UpdateAsync(int id, OpportunityInput input)
    {
        var opp = await (await ScopedAsync()).FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opp == null) return ServiceResult<Opportunity>.From(ServiceResult.NotFound());

        var check = await ValidateAsync(input, opp);
        var assignee = opp.AssignedTo;
        if (!CurrentUser.IsSalesExecutive)
        {
            ServiceResult assignResult;
            (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
            foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        }
        if (!check.Succeeded) return ServiceResult<Opportunity>.From(check);

        Apply(opp, input, assignee);
        await Db.SaveChangesAsync();
        return ServiceResult<Opportunity>.Ok(opp);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var opp = await (await ScopedAsync()).FirstOrDefaultAsync(o => o.OpportunityId == id);
        if (opp == null) return ServiceResult.NotFound();
        opp.IsDeleted = true;
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    /// <summary>Business rules from spec §4.8 / §5.3, enforced regardless of client-side checks.</summary>
    private async Task<ServiceResult> ValidateAsync(OpportunityInput input, Opportunity? existing)
    {
        input.OpportunityName = input.OpportunityName?.Trim() ?? string.Empty;
        var result = ValidateInput(input);
        if (!result.Succeeded) return result;

        var stage = input.Stage!.Value;
        var active = Opportunity.StatusFor(stage) == OpportunityStatus.Open;

        if (input.Amount <= 0)
            result.AddError(nameof(input.Amount), ValidationRules.AmountMessage);
        if (input.Probability is < 0 or > 100)
            result.AddError(nameof(input.Probability), ValidationRules.ProbabilityMessage);
        if (active && input.ExpectedCloseDate!.Value.Date < DateTime.Today)
            result.AddError(nameof(input.ExpectedCloseDate), ValidationRules.CloseDateMessage);

        if (existing is { Status: not OpportunityStatus.Open } && active && !CurrentUser.IsAdmin && !CurrentUser.IsManager)
            result.AddError(nameof(input.Stage), "Only a manager can reopen a closed opportunity.");

        // Referenced records must exist and be inside the user's scope.
        var scope = await CurrentUser.GetScopeAsync();
        if (!await Db.Customers.VisibleTo(scope).AnyAsync(c => c.CustomerId == input.CustomerId))
            result.AddError(nameof(input.CustomerId), "Select a valid customer.");
        if (input.LeadId.HasValue && !await Db.Leads.VisibleTo(scope).AnyAsync(l => l.LeadId == input.LeadId))
            result.AddError(nameof(input.LeadId), "Select a valid lead.");

        return result;
    }

    private static void Apply(Opportunity o, OpportunityInput input, string? assignee)
    {
        var stage = input.Stage!.Value;
        var newStatus = Opportunity.StatusFor(stage);

        o.OpportunityName = input.OpportunityName;
        o.CustomerId = input.CustomerId!.Value;
        o.LeadId = input.LeadId;
        o.Amount = input.Amount!.Value;
        o.Stage = stage;
        o.ExpectedCloseDate = input.ExpectedCloseDate!.Value.Date;
        o.Notes = Clean(input.Notes);
        o.AssignedTo = assignee;

        // Closing an opportunity fixes its probability and records when the outcome happened.
        o.Probability = newStatus switch
        {
            OpportunityStatus.Won => 100,
            OpportunityStatus.Lost => 0,
            _ => input.Probability!.Value
        };
        if (newStatus != OpportunityStatus.Open && o.Status == OpportunityStatus.Open) o.ClosedDate = DateTime.UtcNow;
        if (newStatus == OpportunityStatus.Open) o.ClosedDate = null;
        o.Status = newStatus;
    }
}
