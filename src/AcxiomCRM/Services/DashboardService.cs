using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

/// <summary>Builds KPI cards and chart series from data the current user is authorised to see.</summary>
public class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DashboardService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<DashboardViewModel> BuildAsync(string? range, DateTime? from, DateTime? to)
    {
        var vm = new DashboardViewModel { Range = range ?? "all" };
        (vm.From, vm.To) = ResolveRange(vm.Range, from, to);
        vm.RoleLabel = _currentUser.IsAdmin ? "Organisation-wide" : _currentUser.IsManager ? "Your team" : "Your assigned records";
        vm.Heading = _currentUser.IsAdmin ? "Organisation Dashboard" : _currentUser.IsManager ? "Team Dashboard" : "My Dashboard";
        vm.UserFullName = await _db.Users.Where(u => u.Id == _currentUser.UserId).Select(u => u.FullName).FirstOrDefaultAsync();

        var scope = await _currentUser.GetScopeAsync();
        var customers = InRange(_db.Customers.VisibleTo(scope), vm.From, vm.To);
        var leads = InRange(_db.Leads.VisibleTo(scope), vm.From, vm.To);
        var opps = InRange(_db.Opportunities.VisibleTo(scope), vm.From, vm.To);
        var followUps = _db.FollowUps.VisibleTo(scope).Where(f => f.Status == FollowUpStatus.Planned);
        var today = DateTime.Today;

        vm.TotalCustomers = await customers.CountAsync();
        vm.TotalLeads = await leads.CountAsync();
        vm.OpenLeads = await leads.CountAsync(l => l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost && l.Status != LeadStatus.Unqualified);
        vm.TotalOpportunities = await opps.CountAsync();
        vm.OpenOpportunities = await opps.CountAsync(o => o.Status == OpportunityStatus.Open);
        vm.WonOpportunities = await opps.CountAsync(o => o.Status == OpportunityStatus.Won);
        vm.LostOpportunities = await opps.CountAsync(o => o.Status == OpportunityStatus.Lost);

        var open = await opps.Where(o => o.Status == OpportunityStatus.Open)
            .Select(o => new { o.Amount, o.Probability }).ToListAsync();
        vm.TotalPipelineValue = open.Sum(o => o.Amount);
        vm.WeightedPipelineValue = open.Sum(o => o.Amount * o.Probability / 100m);

        vm.PendingFollowUps = await followUps.CountAsync(f => f.FollowUpDate >= today);
        vm.OverdueFollowUps = await followUps.CountAsync(f => f.FollowUpDate < today);

        // Lead status chart
        var leadCounts = await leads.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        foreach (var status in Enum.GetValues<LeadStatus>())
        {
            vm.LeadStatusChart.Labels.Add(status.DisplayName());
            vm.LeadStatusChart.Values.Add(leadCounts.FirstOrDefault(x => x.Key == status)?.Count ?? 0);
        }

        // Opportunity pipeline chart (count and amount per stage)
        var stageRows = await opps.Select(o => new { o.Stage, o.Amount }).ToListAsync();
        foreach (var stage in Enum.GetValues<OpportunityStage>())
        {
            var rows = stageRows.Where(r => r.Stage == stage).ToList();
            vm.PipelineCountChart.Labels.Add(stage.DisplayName());
            vm.PipelineCountChart.Values.Add(rows.Count);
            vm.PipelineAmountChart.Labels.Add(stage.DisplayName());
            vm.PipelineAmountChart.Values.Add(rows.Sum(r => r.Amount));
        }

        // Monthly sales: won vs lost amounts by close month, last 12 months
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var closed = await _db.Opportunities.VisibleTo(scope)
            .Where(o => o.Status != OpportunityStatus.Open && o.ClosedDate >= start)
            .Select(o => new { o.Status, o.Amount, o.ClosedDate })
            .ToListAsync();
        for (var m = start; m <= today; m = m.AddMonths(1))
        {
            var label = m.ToString("MMM yyyy");
            var inMonth = closed.Where(c => c.ClosedDate!.Value.ToLocalTime().Year == m.Year && c.ClosedDate.Value.ToLocalTime().Month == m.Month).ToList();
            vm.MonthlyWonChart.Labels.Add(label);
            vm.MonthlyWonChart.Values.Add(inMonth.Where(c => c.Status == OpportunityStatus.Won).Sum(c => c.Amount));
            vm.MonthlyLostChart.Labels.Add(label);
            vm.MonthlyLostChart.Values.Add(inMonth.Where(c => c.Status == OpportunityStatus.Lost).Sum(c => c.Amount));
        }

        vm.UpcomingFollowUps = await followUps.Where(f => f.FollowUpDate >= today)
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser)
            .OrderBy(f => f.FollowUpDate).Take(6).AsNoTracking().ToListAsync();
        vm.OverdueFollowUpList = await followUps.Where(f => f.FollowUpDate < today)
            .Include(f => f.Customer).Include(f => f.Lead).Include(f => f.AssignedUser)
            .OrderBy(f => f.FollowUpDate).Take(6).AsNoTracking().ToListAsync();
        vm.ClosingSoon = await _db.Opportunities.VisibleTo(scope)
            .Where(o => o.Status == OpportunityStatus.Open)
            .Include(o => o.Customer)
            .OrderBy(o => o.ExpectedCloseDate).Take(6).AsNoTracking().ToListAsync();

        vm.TodayFollowUps = await followUps.CountAsync(f => f.FollowUpDate == today && f.AssignedTo == _currentUser.UserId);

        if (_currentUser.IsAdmin || _currentUser.IsManager)
            vm.TeamPerformance = await BuildTeamPerformanceAsync(customers, leads, opps, followUps, today);

        if (_currentUser.IsAdmin)
        {
            vm.RecentActivity = await _db.AuditLogs.AsNoTracking().OrderByDescending(a => a.AuditLogId).Take(8).ToListAsync();
            var now = DateTimeOffset.UtcNow;
            var dayStart = DateTime.UtcNow.Date;
            vm.ActiveUsers = await _db.Users.CountAsync(u => u.IsActive);
            vm.LockedOutUsers = (await _db.Users.Where(u => u.LockoutEnd != null).Select(u => u.LockoutEnd).ToListAsync())
                .Count(end => end > now);
            vm.FailedLoginsToday = await _db.AuditLogs.CountAsync(a => a.Action == AuditActions.LoginFailed && a.CreatedDate >= dayStart);
        }

        return vm;
    }

    /// <summary>One row per sales rep: Manager sees direct reports, Admin sees every Sales Executive.</summary>
    private async Task<List<TeamMemberRow>> BuildTeamPerformanceAsync(IQueryable<Customer> customers, IQueryable<Lead> leads,
        IQueryable<Opportunity> opps, IQueryable<FollowUp> plannedFollowUps, DateTime today)
    {
        var reps = _currentUser.IsAdmin
            ? await (from u in _db.Users
                     join ur in _db.UserRoles on u.Id equals ur.UserId
                     join r in _db.Roles on ur.RoleId equals r.Id
                     where r.Name == Roles.SalesExecutive
                     select new { u.Id, u.FullName, u.IsActive }).ToListAsync()
            : await _db.Users.Where(u => u.ManagerId == _currentUser.UserId)
                .Select(u => new { u.Id, u.FullName, u.IsActive }).ToListAsync();
        if (reps.Count == 0) return new();

        var customerCounts = await customers.Where(c => c.AssignedTo != null).GroupBy(c => c.AssignedTo!)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var openLeadCounts = await leads
            .Where(l => l.AssignedTo != null && l.Status != LeadStatus.Converted && l.Status != LeadStatus.Lost && l.Status != LeadStatus.Unqualified)
            .GroupBy(l => l.AssignedTo!).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var overdueCounts = await plannedFollowUps.Where(f => f.AssignedTo != null && f.FollowUpDate < today)
            .GroupBy(f => f.AssignedTo!).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var oppRows = await opps.Where(o => o.AssignedTo != null)
            .Select(o => new { o.AssignedTo, o.Status, o.Amount }).ToListAsync();

        return reps.Select(r =>
            {
                var mine = oppRows.Where(o => o.AssignedTo == r.Id).ToList();
                var open = mine.Where(o => o.Status == OpportunityStatus.Open).ToList();
                var won = mine.Where(o => o.Status == OpportunityStatus.Won).ToList();
                return new TeamMemberRow(r.Id, r.FullName, r.IsActive,
                    customerCounts.GetValueOrDefault(r.Id), openLeadCounts.GetValueOrDefault(r.Id),
                    open.Count, open.Sum(o => o.Amount), won.Count, won.Sum(o => o.Amount),
                    overdueCounts.GetValueOrDefault(r.Id));
            })
            .OrderByDescending(t => t.WonAmount).ThenByDescending(t => t.OpenPipeline)
            .ToList();
    }

    /// <summary>Pipeline report data for the REST API: open amount and weighted amount by stage and owner.</summary>
    public async Task<object> GetPipelineReportAsync()
    {
        var scope = await _currentUser.GetScopeAsync();
        var rows = await _db.Opportunities.VisibleTo(scope).Include(o => o.AssignedUser)
            .Select(o => new { o.Stage, o.Status, o.Amount, o.Probability, Owner = o.AssignedUser != null ? o.AssignedUser.FullName : "Unassigned" })
            .ToListAsync();

        return new
        {
            GeneratedAt = DateTime.UtcNow,
            ByStage = Enum.GetValues<OpportunityStage>().Select(s => new
            {
                Stage = s.ToString(),
                Count = rows.Count(r => r.Stage == s),
                Amount = rows.Where(r => r.Stage == s).Sum(r => r.Amount),
                WeightedAmount = rows.Where(r => r.Stage == s).Sum(r => r.Amount * r.Probability / 100m)
            }),
            ByOwner = rows.Where(r => r.Status == OpportunityStatus.Open).GroupBy(r => r.Owner).Select(g => new
            {
                Owner = g.Key,
                OpenCount = g.Count(),
                Amount = g.Sum(r => r.Amount),
                WeightedAmount = g.Sum(r => r.Amount * r.Probability / 100m)
            }).OrderByDescending(x => x.Amount)
        };
    }

    private static (DateTime? From, DateTime? To) ResolveRange(string range, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        return range switch
        {
            "today" => (today, today),
            "week" => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today), // Monday-based week
            "month" => (new DateTime(today.Year, today.Month, 1), today),
            "custom" => (from?.Date, to?.Date),
            _ => (null, null)
        };
    }

    /// <summary>Filters on CreatedDate (stored in UTC) using local-day boundaries.</summary>
    private static IQueryable<T> InRange<T>(IQueryable<T> q, DateTime? from, DateTime? to) where T : AuditableEntity
    {
        if (from.HasValue)
        {
            var f = from.Value.Date.ToUniversalTime();
            q = q.Where(e => e.CreatedDate >= f);
        }
        if (to.HasValue)
        {
            var t = to.Value.Date.AddDays(1).ToUniversalTime();
            q = q.Where(e => e.CreatedDate < t);
        }
        return q;
    }
}
