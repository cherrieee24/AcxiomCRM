namespace AcxiomCRM.Infrastructure;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string SalesExecutive = "SalesExecutive";

    public const string AdminOrManager = Admin + "," + Manager;

    public static readonly string[] All = { Admin, Manager, SalesExecutive };

    public static string DisplayName(string role) => role == SalesExecutive ? "Sales Executive" : role;
}
