using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

public class OpportunityInput
{
    [Required(ErrorMessage = "Opportunity Name is required.")]
    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Opportunity Name")]
    public string OpportunityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Customer is required.")]
    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Source Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, ValidationRules.MaxAmount, ErrorMessage = ValidationRules.AmountMessage)]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Stage is required.")]
    public OpportunityStage? Stage { get; set; } = OpportunityStage.Qualification;

    [Required(ErrorMessage = "Probability is required.")]
    [Range(0, 100, ErrorMessage = ValidationRules.ProbabilityMessage)]
    [Display(Name = "Probability (%)")]
    public int? Probability { get; set; }

    // Closed (Won/Lost) opportunities may keep a historical date; active ones may not.
    [Required(ErrorMessage = "Expected Close Date is required.")]
    [DataType(DataType.Date)]
    [NotInPast(UnlessProperty = nameof(Stage), UnlessValues = "Won,Lost", ErrorMessage = ValidationRules.CloseDateMessage)]
    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; }

    [StringLength(1000, ErrorMessage = ValidationRules.LengthMessage)]
    [DataType(DataType.MultilineText)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    public static OpportunityInput From(Opportunity o) => new()
    {
        OpportunityName = o.OpportunityName,
        CustomerId = o.CustomerId,
        LeadId = o.LeadId,
        Amount = o.Amount,
        Stage = o.Stage,
        Probability = o.Probability,
        ExpectedCloseDate = o.ExpectedCloseDate,
        Notes = o.Notes,
        AssignedTo = o.AssignedTo
    };
}

public record OpportunityDto(
    int OpportunityId,
    string OpportunityName,
    int CustomerId,
    string? CustomerName,
    int? LeadId,
    decimal Amount,
    OpportunityStage Stage,
    int Probability,
    decimal WeightedAmount,
    DateTime ExpectedCloseDate,
    OpportunityStatus Status,
    DateTime? ClosedDate,
    string? AssignedTo,
    string? AssignedToName,
    DateTime CreatedDate)
{
    public static OpportunityDto From(Opportunity o) => new(
        o.OpportunityId, o.OpportunityName, o.CustomerId, o.Customer?.CustomerName, o.LeadId, o.Amount, o.Stage,
        o.Probability, o.WeightedAmount, o.ExpectedCloseDate, o.Status, o.ClosedDate, o.AssignedTo,
        o.AssignedUser?.FullName, o.CreatedDate);
}
