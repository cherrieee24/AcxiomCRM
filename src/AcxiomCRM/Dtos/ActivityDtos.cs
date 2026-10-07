using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

public class ActivityInput
{
    [Required(ErrorMessage = "Activity Type is required.")]
    [Display(Name = "Type")]
    public ActivityType? ActivityType { get; set; } = Models.ActivityType.Call;

    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = ValidationRules.LengthMessage)]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Activity Date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Activity Date")]
    public DateTime? ActivityDate { get; set; } = DateTime.Today;

    [Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Display(Name = "Lead")]
    public int? LeadId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public ActivityStatus? Status { get; set; } = ActivityStatus.Planned;

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    public static ActivityInput From(Activity a) => new()
    {
        ActivityType = a.ActivityType,
        Subject = a.Subject,
        Description = a.Description,
        ActivityDate = a.ActivityDate,
        CustomerId = a.CustomerId,
        LeadId = a.LeadId,
        Status = a.Status,
        AssignedTo = a.AssignedTo
    };
}
