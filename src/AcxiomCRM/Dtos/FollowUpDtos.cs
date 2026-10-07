using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

public class FollowUpInput : IValidatableObject
{
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Follow-up Type is required.")]
    [Display(Name = "Type")]
    public FollowUpType? FollowUpType { get; set; } = Models.FollowUpType.Call;

    [Required(ErrorMessage = "Follow-up Date is required.")]
    [DataType(DataType.Date)]
    [NotInPast(ErrorMessage = ValidationRules.FollowUpDateMessage)]
    [Display(Name = "Follow-up Date")]
    public DateTime? FollowUpDate { get; set; } = DateTime.Today;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Display(Name = "Opportunity")]
    public int? OpportunityId { get; set; }

    [StringLength(1000, ErrorMessage = ValidationRules.LengthMessage)]
    [DataType(DataType.MultilineText)]
    public string? Remarks { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CustomerId == null && LeadId == null && OpportunityId == null)
            yield return new ValidationResult("Link the follow-up to a customer, lead or opportunity.", new[] { nameof(CustomerId) });
    }

    public static FollowUpInput From(FollowUp f) => new()
    {
        Subject = f.Subject,
        FollowUpType = f.FollowUpType,
        FollowUpDate = f.FollowUpDate,
        CustomerId = f.CustomerId,
        LeadId = f.LeadId,
        OpportunityId = f.OpportunityId,
        Remarks = f.Remarks,
        AssignedTo = f.AssignedTo
    };
}

/// <summary>Move a planned/missed follow-up to a new date.</summary>
public class FollowUpRescheduleInput
{
    [Required(ErrorMessage = "New date is required.")]
    [DataType(DataType.Date)]
    [NotInPast(ErrorMessage = ValidationRules.FollowUpDateMessage)]
    [Display(Name = "New Follow-up Date")]
    public DateTime? FollowUpDate { get; set; }

    [StringLength(1000, ErrorMessage = ValidationRules.LengthMessage)]
    public string? Remarks { get; set; }
}

public record FollowUpDto(
    int FollowUpId,
    string Subject,
    FollowUpType FollowUpType,
    DateTime FollowUpDate,
    FollowUpStatus Status,
    bool IsOverdue,
    int? CustomerId,
    string? CustomerName,
    int? LeadId,
    string? LeadName,
    int? OpportunityId,
    string? Remarks,
    string? AssignedTo,
    string? AssignedToName)
{
    public static FollowUpDto From(FollowUp f) => new(
        f.FollowUpId, f.Subject, f.FollowUpType, f.FollowUpDate, f.Status, f.IsOverdue, f.CustomerId,
        f.Customer?.CustomerName, f.LeadId, f.Lead?.LeadName, f.OpportunityId, f.Remarks, f.AssignedTo,
        f.AssignedUser?.FullName);
}
