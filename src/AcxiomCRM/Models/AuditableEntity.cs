namespace AcxiomCRM.Models;

/// <summary>
/// Common tracking columns. Populated automatically by ApplicationDbContext.SaveChangesAsync,
/// which also writes the matching AuditLog rows.
/// </summary>
public abstract class AuditableEntity
{
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Soft-delete flag; deleted rows are hidden by a global query filter.</summary>
    public bool IsDeleted { get; set; }
}

/// <summary>A CRM record owned by (assigned to) a user. Drives role-based data scoping.</summary>
public interface IAssignable
{
    string? AssignedTo { get; set; }
}
