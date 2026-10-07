using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

public class LeadInput
{
    [Required(ErrorMessage = "Lead Name is required.")]
    [StringLength(100, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Lead Name")]
    public string LeadName { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [RegularExpression(ValidationRules.EmailPattern, ErrorMessage = ValidationRules.EmailMessage)]
    [DataType(DataType.EmailAddress)]
    public string? Email { get; set; }

    [RegularExpression(ValidationRules.PhonePattern, ErrorMessage = ValidationRules.PhoneMessage)]
    [DataType(DataType.PhoneNumber)]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Company")]
    public string? CompanyName { get; set; }

    [Required(ErrorMessage = "Lead Source is required.")]
    public LeadSource? Source { get; set; }

    [Required(ErrorMessage = "Lead Status is required.")]
    public LeadStatus? Status { get; set; } = LeadStatus.New;

    [Required(ErrorMessage = "Priority is required.")]
    public Priority? Priority { get; set; } = Models.Priority.Medium;

    [Required(ErrorMessage = "Expected Value is required.")]
    [Range(0, ValidationRules.MaxLeadValue, ErrorMessage = "Expected Value must be between 0 and 100,000,000.")]
    [Display(Name = "Expected Value")]
    public decimal? ExpectedValue { get; set; }

    [StringLength(1000, ErrorMessage = ValidationRules.LengthMessage)]
    [DataType(DataType.MultilineText)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    public static LeadInput From(Lead l) => new()
    {
        LeadName = l.LeadName,
        Email = l.Email,
        Phone = l.Phone,
        CompanyName = l.CompanyName,
        Source = l.Source,
        Status = l.Status,
        Priority = l.Priority,
        ExpectedValue = l.ExpectedValue,
        Notes = l.Notes,
        AssignedTo = l.AssignedTo
    };
}

/// <summary>Options when converting a qualified lead into a customer (and optionally an opportunity).</summary>
public class LeadConvertInput
{
    [Display(Name = "Also create an opportunity")]
    public bool CreateOpportunity { get; set; } = true;

    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Opportunity Name")]
    public string? OpportunityName { get; set; }

    [Range(0.01, ValidationRules.MaxAmount, ErrorMessage = ValidationRules.AmountMessage)]
    public decimal? Amount { get; set; }

    [Range(0, 100, ErrorMessage = ValidationRules.ProbabilityMessage)]
    [Display(Name = "Probability (%)")]
    public int? Probability { get; set; }

    [DataType(DataType.Date)]
    [NotInPast(ErrorMessage = ValidationRules.CloseDateMessage)]
    [Display(Name = "Expected Close Date")]
    public DateTime? ExpectedCloseDate { get; set; }
}

public record LeadDto(
    int LeadId,
    string LeadCode,
    string LeadName,
    string? Email,
    string? Phone,
    string? CompanyName,
    LeadSource Source,
    LeadStatus Status,
    Priority Priority,
    decimal ExpectedValue,
    string? AssignedTo,
    string? AssignedToName,
    int? ConvertedCustomerId,
    DateTime? ConvertedDate,
    DateTime CreatedDate)
{
    public static LeadDto From(Lead l) => new(
        l.LeadId, l.LeadCode, l.LeadName, l.Email, l.Phone, l.CompanyName, l.Source, l.Status, l.Priority,
        l.ExpectedValue, l.AssignedTo, l.AssignedUser?.FullName, l.ConvertedCustomerId, l.ConvertedDate, l.CreatedDate);
}
