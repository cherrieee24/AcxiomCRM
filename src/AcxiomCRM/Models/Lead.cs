using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.Models;

public class Lead : AuditableEntity, IAssignable
{
    public int LeadId { get; set; }

    [Required, StringLength(20)]
    public string LeadCode { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LeadName { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(15)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? CompanyName { get; set; }

    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    public Priority Priority { get; set; } = Priority.Medium;

    public decimal ExpectedValue { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public string? AssignedTo { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    // Conversion tracking
    public DateTime? ConvertedDate { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public Customer? ConvertedCustomer { get; set; }

    public ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
