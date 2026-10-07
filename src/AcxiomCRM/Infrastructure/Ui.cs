using System.Globalization;
using System.Security.Claims;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.WebUtilities;

namespace AcxiomCRM.Infrastructure;

/// <summary>Small formatting helpers shared by Razor views.</summary>
public static class Ui
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    public static string Money(decimal amount) => "₹" + amount.ToString("N0", India);

    public static string Date(DateTime date) => date.ToString("dd MMM yyyy");

    /// <summary>Timestamps are stored in UTC; show them in server local time.</summary>
    public static string Timestamp(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd MMM yyyy, HH:mm");

    public static string Badge(Enum value) => "badge " + value switch
    {
        CustomerStatus.Active or LeadStatus.Converted or OpportunityStage.Won or OpportunityStatus.Won
            or FollowUpStatus.Completed or ActivityStatus.Completed => "bg-success",
        CustomerStatus.Inactive or LeadStatus.Lost or OpportunityStage.Lost or OpportunityStatus.Lost
            or FollowUpStatus.Missed => "bg-danger",
        CustomerStatus.OnHold or LeadStatus.Unqualified or FollowUpStatus.Cancelled or ActivityStatus.Cancelled => "bg-secondary",
        LeadStatus.New or OpportunityStage.Qualification => "bg-info text-dark",
        LeadStatus.Contacted or OpportunityStage.Proposal => "bg-primary",
        LeadStatus.Qualified or OpportunityStage.Negotiation => "bg-warning text-dark",
        Priority.High => "bg-danger",
        Priority.Medium => "bg-warning text-dark",
        Priority.Low => "bg-light text-dark border",
        _ => "bg-light text-dark border"
    };

    /// <summary>"All Customers" (Admin), "Team Customers" (Manager), "My Customers" (Sales Executive).</summary>
    public static string ScopeTitle(ClaimsPrincipal user, string noun) =>
        (user.IsInRole(Roles.Admin) ? "All " : user.IsInRole(Roles.Manager) ? "Team " : "My ") + noun;

    public static string ScopeHint(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) ? "Showing every record in the organisation"
        : user.IsInRole(Roles.Manager) ? "Showing records owned by you, your direct reports, and unassigned records"
        : "Showing only records assigned to you";

    /// <summary>CSS class on &lt;body&gt; so each role gets its own accent colour.</summary>
    public static string RoleCss(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) ? "role-admin" : user.IsInRole(Roles.Manager) ? "role-manager"
        : user.Identity?.IsAuthenticated == true ? "role-sales" : "";

    /// <summary>Current URL with some query-string values replaced (null removes the key).</summary>
    public static string WithQuery(HttpRequest request, params (string Key, string? Value)[] changes)
    {
        var values = request.Query.ToDictionary(q => q.Key, q => (string?)q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (value == null) values.Remove(key);
            else values[key] = value;
        }
        return QueryHelpers.AddQueryString(request.PathBase + request.Path, values.Where(v => !string.IsNullOrEmpty(v.Value)));
    }

    public static string SortUrl(HttpRequest request, string key, string? currentSort, bool desc) =>
        WithQuery(request, ("sort", key), ("desc", currentSort == key && !desc ? "true" : "false"), ("page", null));

    public static string SortIcon(string key, string? currentSort, bool desc) =>
        currentSort != key ? "bi-arrow-down-up text-muted" : desc ? "bi-sort-down" : "bi-sort-up";
}
