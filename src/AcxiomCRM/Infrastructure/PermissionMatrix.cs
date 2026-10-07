namespace AcxiomCRM.Infrastructure;

/// <summary>
/// Human-readable description of the access rules (spec §7.1). The rules themselves are enforced by
/// [Authorize(Roles = ...)] attributes and the DataScope applied in every service query.
/// </summary>
public static class PermissionMatrix
{
    public record Row(string Module, string Admin, string Manager, string SalesExecutive);

    public static readonly IReadOnlyDictionary<string, string> RoleDescriptions = new Dictionary<string, string>
    {
        [Roles.Admin] = "Full administration: users, roles, audit logs, all CRM records and reports.",
        [Roles.Manager] = "Manages team customers, leads, opportunities, follow-ups and activities; team reports. No security administration.",
        [Roles.SalesExecutive] = "Works on assigned customers, leads, opportunities, follow-ups and activities."
    };

    public static readonly IReadOnlyList<Row> Rows = new Row[]
    {
        new("Dashboard", "Full", "Team", "Own / Assigned"),
        new("Customers", "Full", "Team + unassigned", "Own / Assigned"),
        new("Leads", "Full", "Team + unassigned", "Own / Assigned"),
        new("Opportunities", "Full", "Team + unassigned", "Own / Assigned"),
        new("Follow-Ups", "Full", "Team + unassigned", "Own / Assigned"),
        new("Activities", "Full", "Team + unassigned", "Own / Assigned"),
        new("Assign records to users", "Anyone", "Team members", "Self only"),
        new("Delete CRM records", "Yes", "Yes (team scope)", "No"),
        new("User Management", "Full", "No", "No"),
        new("Role Management", "Full", "No", "No"),
        new("Audit Log", "Full", "No", "No"),
        new("REST API", "Authorized endpoints", "Authorized endpoints", "Authorized endpoints"),
        new("Pipeline Report API", "All data", "Team data", "No")
    };
}
