using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Models;

/// <summary>
/// ASP.NET Core Identity user extended with CRM fields. Password hashes, lockout counters and
/// security stamps are managed entirely by Identity.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>The manager a Sales Executive reports to. Defines a manager's "team" scope.</summary>
    public string? ManagerId { get; set; }
    public ApplicationUser? Manager { get; set; }
}
