using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class FollowUpService : CrmServiceBase
{
    public FollowUpService(ApplicationDbContext db, ICurrentUserService currentUser) : base(db, currentUser) { }

    private async Task<IQueryable<FollowUp>> ScopedAsync() =>
        Db.FollowUps.VisibleTo(await CurrentUser.GetScopeAsync());

    public async Task<PagedList<FollowUp>> SearchAsync(FollowUpFilter f)
    {
        var q = (await ScopedAsync())
            .Include(x => x.Customer).Include(x => x.Lead).Include(x => x.Opportunity).Include(x => x.AssignedUser)
            .AsNoTracking();

        var today = DateTime.Today;
        q = f.View switch
        {
            "upcoming" => q.Where(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate >= today),
            "overdue" => q.Where(x => x.Status == FollowUpStatus.Planned && x.FollowUpDate < today),
            _ => q
        };

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(x => x.Subject.Contains(s)
                             || (x.Customer != null && x.Customer.CustomerName.Contains(s))
                             || (x.Lead != null && x.Lead.LeadName.Contains(s)));
        }
        if (f.Status.HasValue) q = q.Where(x => x.Status == f.Status);
        if (f.From.HasValue) q = q.Where(x => x.FollowUpDate >= f.From.Value.Date);
        if (f.To.HasValue) q = q.Where(x => x.FollowUpDate < f.To.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(f.AssignedTo)) q = q.Where(x => x.AssignedTo == f.AssignedTo);

        q = (f.Sort, f.Desc) switch
        {
            ("date", true) => q.OrderByDescending(x => x.FollowUpDate),
            ("subject", false) => q.OrderBy(x => x.Subject),
            ("subject", true) => q.OrderByDescending(x => x.Subject),
            _ => q.OrderBy(x => x.FollowUpDate)
        };

        return await PagedList<FollowUp>.CreateAsync(q, f.Page, f.PageSize);
    }

    public async Task<FollowUp?> GetAsync(int id) =>
        await (await ScopedAsync())
            .Include(x => x.Customer).Include(x => x.Lead).Include(x => x.Opportunity).Include(x => x.AssignedUser)
            .FirstOrDefaultAsync(x => x.FollowUpId == id);

    public async Task<ServiceResult<FollowUp>> CreateAsync(FollowUpInput input)
    {
        var check = await ValidateAsync(input);
        var (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
        foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        if (!check.Succeeded) return ServiceResult<FollowUp>.From(check);

        var followUp = new FollowUp { Status = FollowUpStatus.Planned, CreatedBy = CurrentUser.UserId };
        Apply(followUp, input, assignee);
        Db.FollowUps.Add(followUp);
        await Db.SaveChangesAsync();
        return ServiceResult<FollowUp>.Ok(followUp);
    }

    public async Task<ServiceResult<FollowUp>> UpdateAsync(int id, FollowUpInput input)
    {
        var followUp = await (await ScopedAsync()).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (followUp == null) return ServiceResult<FollowUp>.From(ServiceResult.NotFound());
        if (followUp.Status != FollowUpStatus.Planned)
            return ServiceResult<FollowUp>.From(ServiceResult.Invalid(string.Empty, "Only planned follow-ups can be edited."));

        var check = await ValidateAsync(input);
        var assignee = followUp.AssignedTo;
        if (!CurrentUser.IsSalesExecutive)
        {
            ServiceResult assignResult;
            (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
            foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        }
        if (!check.Succeeded) return ServiceResult<FollowUp>.From(check);

        Apply(followUp, input, assignee);
        await Db.SaveChangesAsync();
        return ServiceResult<FollowUp>.Ok(followUp);
    }

    public Task<ServiceResult> CompleteAsync(int id, string? outcome) =>
        ChangeStatusAsync(id, FollowUpStatus.Completed, outcome);

    public Task<ServiceResult> MarkMissedAsync(int id) =>
        ChangeStatusAsync(id, FollowUpStatus.Missed, null);

    public Task<ServiceResult> CancelAsync(int id) =>
        ChangeStatusAsync(id, FollowUpStatus.Cancelled, null);

    /// <summary>Planned or missed follow-ups can be moved to a new (non-past) date and become Planned again.</summary>
    public async Task<ServiceResult> RescheduleAsync(int id, FollowUpRescheduleInput input)
    {
        var check = ValidateInput(input);
        if (!check.Succeeded) return check;

        var followUp = await (await ScopedAsync()).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (followUp == null) return ServiceResult.NotFound();
        if (followUp.Status is not (FollowUpStatus.Planned or FollowUpStatus.Missed))
            return ServiceResult.Invalid(string.Empty, "Only planned or missed follow-ups can be rescheduled.");

        followUp.FollowUpDate = input.FollowUpDate!.Value.Date;
        followUp.Status = FollowUpStatus.Planned;
        followUp.Remarks = AppendRemark(followUp.Remarks, $"Rescheduled to {followUp.FollowUpDate:dd MMM yyyy}. {input.Remarks}".Trim());
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var followUp = await (await ScopedAsync()).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (followUp == null) return ServiceResult.NotFound();
        followUp.IsDeleted = true;
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> ChangeStatusAsync(int id, FollowUpStatus status, string? remark)
    {
        var followUp = await (await ScopedAsync()).FirstOrDefaultAsync(x => x.FollowUpId == id);
        if (followUp == null) return ServiceResult.NotFound();
        if (followUp.Status != FollowUpStatus.Planned)
            return ServiceResult.Invalid(string.Empty, $"This follow-up is already {followUp.Status}.");

        followUp.Status = status;
        if (status == FollowUpStatus.Completed) followUp.CompletedDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(remark))
        {
            if (remark.Length > 500) return ServiceResult.Invalid("outcome", "Outcome cannot exceed 500 characters.");
            followUp.Remarks = AppendRemark(followUp.Remarks, $"Outcome: {remark.Trim()}");
        }
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> ValidateAsync(FollowUpInput input)
    {
        input.Subject = input.Subject?.Trim() ?? string.Empty;
        var result = ValidateInput(input); // includes "date not in past" and "linked to a record"
        if (!result.Succeeded) return result;

        var scope = await CurrentUser.GetScopeAsync();
        if (input.CustomerId.HasValue && !await Db.Customers.VisibleTo(scope).AnyAsync(c => c.CustomerId == input.CustomerId))
            result.AddError(nameof(input.CustomerId), "Select a valid customer.");
        if (input.LeadId.HasValue && !await Db.Leads.VisibleTo(scope).AnyAsync(l => l.LeadId == input.LeadId))
            result.AddError(nameof(input.LeadId), "Select a valid lead.");
        if (input.OpportunityId.HasValue && !await Db.Opportunities.VisibleTo(scope).AnyAsync(o => o.OpportunityId == input.OpportunityId))
            result.AddError(nameof(input.OpportunityId), "Select a valid opportunity.");
        return result;
    }

    private static void Apply(FollowUp f, FollowUpInput input, string? assignee)
    {
        f.Subject = input.Subject;
        f.FollowUpType = input.FollowUpType!.Value;
        f.FollowUpDate = input.FollowUpDate!.Value.Date;
        f.CustomerId = input.CustomerId;
        f.LeadId = input.LeadId;
        f.OpportunityId = input.OpportunityId;
        f.Remarks = Clean(input.Remarks);
        f.AssignedTo = assignee;
    }

    private static string AppendRemark(string? existing, string addition)
    {
        var combined = string.IsNullOrWhiteSpace(existing) ? addition : existing + Environment.NewLine + addition;
        return combined.Length <= 1000 ? combined : combined[^1000..];
    }
}
