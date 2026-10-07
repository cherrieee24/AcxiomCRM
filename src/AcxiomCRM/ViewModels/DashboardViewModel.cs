using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

public class DashboardViewModel
{
    /// <summary>all | today | week | month | custom</summary>
    public string Range { get; set; } = "all";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public string RoleLabel { get; set; } = string.Empty;

    /// <summary>"Organisation Dashboard" / "Team Dashboard" / "My Dashboard".</summary>
    public string Heading { get; set; } = "Dashboard";
    public string? UserFullName { get; set; }

    /// <summary>Sales Executive "My Day": planned follow-ups due today.</summary>
    public int TodayFollowUps { get; set; }

    /// <summary>Per-sales-rep breakdown (Manager: direct reports, Admin: every Sales Executive).</summary>
    public List<TeamMemberRow> TeamPerformance { get; set; } = new();

    /// <summary>Admin only: latest audit events across the system.</summary>
    public List<AuditLog> RecentActivity { get; set; } = new();

    // KPI cards (spec §17.11)
    public int TotalCustomers { get; set; }
    public int TotalLeads { get; set; }
    public int OpenLeads { get; set; }
    public int TotalOpportunities { get; set; }
    public int OpenOpportunities { get; set; }
    public int WonOpportunities { get; set; }
    public int LostOpportunities { get; set; }
    public decimal TotalPipelineValue { get; set; }
    public decimal WeightedPipelineValue { get; set; }
    public int PendingFollowUps { get; set; }
    public int OverdueFollowUps { get; set; }

    // Admin-only security statistics
    public int? ActiveUsers { get; set; }
    public int? LockedOutUsers { get; set; }
    public int? FailedLoginsToday { get; set; }

    // Charts (spec §17.12)
    public ChartData LeadStatusChart { get; set; } = new();
    public ChartData PipelineCountChart { get; set; } = new();
    public ChartData PipelineAmountChart { get; set; } = new();
    public ChartData MonthlyWonChart { get; set; } = new();
    public ChartData MonthlyLostChart { get; set; } = new();

    public List<FollowUp> UpcomingFollowUps { get; set; } = new();
    public List<FollowUp> OverdueFollowUpList { get; set; } = new();
    public List<Opportunity> ClosingSoon { get; set; } = new();
}

public record TeamMemberRow(
    string UserId,
    string Name,
    bool IsActive,
    int Customers,
    int OpenLeads,
    int OpenOpportunities,
    decimal OpenPipeline,
    int WonDeals,
    decimal WonAmount,
    int OverdueFollowUps);

public class ChartData
{
    public List<string> Labels { get; set; } = new();
    public List<decimal> Values { get; set; } = new();
}
