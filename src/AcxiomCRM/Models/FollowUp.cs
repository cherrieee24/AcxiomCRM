using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class FollowUp : AuditableEntity, IAssignable
{
    public int FollowUpId { get; set; }

    // A follow-up relates to at least one of: customer, lead, opportunity.
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public int? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public DateTime FollowUpDate { get; set; }

    [Required, StringLength(150)]
    public string Subject { get; set; } = string.Empty;

    public FollowUpType FollowUpType { get; set; }

    public FollowUpStatus Status { get; set; } = FollowUpStatus.Planned;

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public DateTime? CompletedDate { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public bool IsOverdue => Status == FollowUpStatus.Planned && FollowUpDate.Date < DateTime.Today;
}
