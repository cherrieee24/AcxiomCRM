using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels;

/// <summary>Common paging/sorting query-string parameters for list pages and API list endpoints.</summary>
public abstract class ListFilter
{
    public string? Search { get; set; }
    public string? AssignedTo { get; set; }
    public string? Sort { get; set; }
    public bool Desc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public class CustomerFilter : ListFilter
{
    public CustomerStatus? Status { get; set; }
}

public class LeadFilter : ListFilter
{
    public LeadStatus? Status { get; set; }
    public LeadSource? Source { get; set; }
}

public class OpportunityFilter : ListFilter
{
    public OpportunityStage? Stage { get; set; }
    public OpportunityStatus? Status { get; set; }
    public int? CustomerId { get; set; }
}

public class FollowUpFilter : ListFilter
{
    public FollowUpStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>"upcoming", "overdue" or empty for all.</summary>
    public string? View { get; set; }
}

public class ActivityFilter : ListFilter
{
    public ActivityType? Type { get; set; }
    public ActivityStatus? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class AuditLogFilter
{
    public string? UserName { get; set; }
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
