using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

/// <summary>
/// Append-only audit record. ApplicationDbContext refuses to update or delete these rows.
/// Never store passwords, hashes or tokens in OldValue/NewValue.
/// </summary>
public class AuditLog
{
    public long AuditLogId { get; set; }

    public string? UserId { get; set; }

    [StringLength(256)]
    public string? UserName { get; set; }

    /// <summary>Login, LoginFailed, Lockout, Logout, Create, Update, Delete, RoleChange, Security...</summary>
    [Required, StringLength(50)]
    public string Action { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? RecordId { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    /// <summary>Success / Failure.</summary>
    [StringLength(20)]
    public string Result { get; set; } = "Success";

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [StringLength(64)]
    public string? IpAddress { get; set; }
}

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Lockout = "Lockout";
    public const string Logout = "Logout";
    public const string Register = "Register";
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string StatusChange = "StatusChange";
    public const string Convert = "Convert";
    public const string RoleChange = "RoleChange";
    public const string Security = "Security";

    public static readonly string[] All =
        { Login, LoginFailed, Lockout, Logout, Register, Create, Update, Delete, StatusChange, Convert, RoleChange, Security };
}
