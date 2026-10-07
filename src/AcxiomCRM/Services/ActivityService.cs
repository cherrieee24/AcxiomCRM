using AcxiomCRM.Data;
using AcxiomCRM.Dtos;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public class ActivityService : CrmServiceBase
{
    public ActivityService(ApplicationDbContext db, ICurrentUserService currentUser) : base(db, currentUser) { }

    private async Task<IQueryable<Activity>> ScopedAsync() =>
        Db.Activities.VisibleTo(await CurrentUser.GetScopeAsync());

    public async Task<PagedList<Activity>> SearchAsync(ActivityFilter f)
    {
        var q = (await ScopedAsync()).Include(a => a.Customer).Include(a => a.Lead).Include(a => a.AssignedUser).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            q = q.Where(a => a.Subject.Contains(s)
                             || (a.Customer != null && a.Customer.CustomerName.Contains(s))
                             || (a.Lead != null && a.Lead.LeadName.Contains(s)));
        }
        if (f.Type.HasValue) q = q.Where(a => a.ActivityType == f.Type);
        if (f.Status.HasValue) q = q.Where(a => a.Status == f.Status);
        if (f.From.HasValue) q = q.Where(a => a.ActivityDate >= f.From.Value.Date);
        if (f.To.HasValue) q = q.Where(a => a.ActivityDate < f.To.Value.Date.AddDays(1));
        if (!string.IsNullOrEmpty(f.AssignedTo)) q = q.Where(a => a.AssignedTo == f.AssignedTo);

        q = (f.Sort, f.Desc) switch
        {
            ("date", false) => q.OrderBy(a => a.ActivityDate),
            ("subject", false) => q.OrderBy(a => a.Subject),
            ("subject", true) => q.OrderByDescending(a => a.Subject),
            _ => q.OrderByDescending(a => a.ActivityDate)
        };

        return await PagedList<Activity>.CreateAsync(q, f.Page, f.PageSize);
    }

    public async Task<Activity?> GetAsync(int id) =>
        await (await ScopedAsync()).Include(a => a.Customer).Include(a => a.Lead).Include(a => a.AssignedUser)
            .FirstOrDefaultAsync(a => a.ActivityId == id);

    public async Task<ServiceResult<Activity>> CreateAsync(ActivityInput input)
    {
        var check = await ValidateAsync(input);
        var (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
        foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        if (!check.Succeeded) return ServiceResult<Activity>.From(check);

        var activity = new Activity { CreatedBy = CurrentUser.UserId };
        Apply(activity, input, assignee);
        Db.Activities.Add(activity);
        await Db.SaveChangesAsync();
        return ServiceResult<Activity>.Ok(activity);
    }

    public async Task<ServiceResult<Activity>> UpdateAsync(int id, ActivityInput input)
    {
        var activity = await (await ScopedAsync()).FirstOrDefaultAsync(a => a.ActivityId == id);
        if (activity == null) return ServiceResult<Activity>.From(ServiceResult.NotFound());

        var check = await ValidateAsync(input);
        var assignee = activity.AssignedTo;
        if (!CurrentUser.IsSalesExecutive)
        {
            ServiceResult assignResult;
            (assignee, assignResult) = await ResolveAssigneeAsync(input.AssignedTo);
            foreach (var (k, v) in assignResult.Errors) check.AddError(k, v);
        }
        if (!check.Succeeded) return ServiceResult<Activity>.From(check);

        Apply(activity, input, assignee);
        await Db.SaveChangesAsync();
        return ServiceResult<Activity>.Ok(activity);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var activity = await (await ScopedAsync()).FirstOrDefaultAsync(a => a.ActivityId == id);
        if (activity == null) return ServiceResult.NotFound();
        activity.IsDeleted = true;
        await Db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> ValidateAsync(ActivityInput input)
    {
        input.Subject = input.Subject?.Trim() ?? string.Empty;
        var result = ValidateInput(input);
        if (!result.Succeeded) return result;

        var scope = await CurrentUser.GetScopeAsync();
        if (input.CustomerId.HasValue && !await Db.Customers.VisibleTo(scope).AnyAsync(c => c.CustomerId == input.CustomerId))
            result.AddError(nameof(input.CustomerId), "Select a valid customer.");
        if (input.LeadId.HasValue && !await Db.Leads.VisibleTo(scope).AnyAsync(l => l.LeadId == input.LeadId))
            result.AddError(nameof(input.LeadId), "Select a valid lead.");
        return result;
    }

    private static void Apply(Activity a, ActivityInput input, string? assignee)
    {
        a.ActivityType = input.ActivityType!.Value;
        a.Subject = input.Subject;
        a.Description = Clean(input.Description);
        a.ActivityDate = input.ActivityDate!.Value.Date;
        a.CustomerId = input.CustomerId;
        a.LeadId = input.LeadId;
        a.Status = input.Status!.Value;
        a.AssignedTo = assignee;
    }
}
