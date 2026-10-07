using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Activity : AuditableEntity, IAssignable
{
    public int ActivityId { get; set; }

    public ActivityType ActivityType { get; set; }

    [Required, StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }
}
