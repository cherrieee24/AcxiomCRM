using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Opportunity : AuditableEntity, IAssignable
{
    public int OpportunityId { get; set; }

    [Required, StringLength(150)]
    public string OpportunityName { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public int? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public decimal Amount { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Qualification;

    /// <summary>0-100 (%).</summary>
    public int Probability { get; set; }

    public DateTime ExpectedCloseDate { get; set; }

    /// <summary>Derived from Stage: Won/Lost close the opportunity, everything else is Open.</summary>
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;

    /// <summary>Set when the opportunity moves to Won or Lost.</summary>
    public DateTime? ClosedDate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();

    public decimal WeightedAmount => Amount * Probability / 100m;

    public static OpportunityStatus StatusFor(OpportunityStage stage) => stage switch
    {
        OpportunityStage.Won => OpportunityStatus.Won,
        OpportunityStage.Lost => OpportunityStatus.Lost,
        _ => OpportunityStatus.Open
    };
}
