using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;

namespace AcxiomCRM.ViewModels;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class UserListViewModel
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? Active { get; set; }
    public List<UserListItemViewModel> Users { get; set; } = new();
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [RegularExpression(ValidationRules.EmailPattern, ErrorMessage = ValidationRules.EmailMessage)]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = Roles.SalesExecutive;

    [Display(Name = "Reports To (Manager)")]
    public string? ManagerId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // Only used when creating a user.
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Initial Password")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string? ConfirmPassword { get; set; }

    public bool IsNew => string.IsNullOrEmpty(Id);
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = string.Empty;
    public string? FullName { get; set; }

    [Required(ErrorMessage = "New password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least {2} characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm the new password.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RoleSummaryViewModel
{
    public string Role { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<RoleMemberViewModel> Members { get; set; } = new();
}

public record RoleMemberViewModel(string Id, string FullName, string Email, bool IsActive);
