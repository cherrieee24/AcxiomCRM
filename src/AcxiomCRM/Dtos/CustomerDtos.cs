using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;

namespace AcxiomCRM.Dtos;

/// <summary>Create/update payload for customers (MVC form and REST API).</summary>
public class CustomerInput
{
    [Required(ErrorMessage = "Customer Name is required.")]
    [StringLength(100, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [RegularExpression(ValidationRules.EmailPattern, ErrorMessage = ValidationRules.EmailMessage)]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone is required.")]
    [RegularExpression(ValidationRules.PhonePattern, ErrorMessage = ValidationRules.PhoneMessage)]
    [DataType(DataType.PhoneNumber)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150, ErrorMessage = ValidationRules.LengthMessage)]
    [Display(Name = "Company")]
    public string? CompanyName { get; set; }

    [StringLength(250, ErrorMessage = ValidationRules.LengthMessage)]
    public string? Address { get; set; }

    [StringLength(80, ErrorMessage = ValidationRules.LengthMessage)]
    public string? City { get; set; }

    [StringLength(80, ErrorMessage = ValidationRules.LengthMessage)]
    public string? State { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    public CustomerStatus? Status { get; set; } = CustomerStatus.Active;

    [StringLength(1000, ErrorMessage = ValidationRules.LengthMessage)]
    [DataType(DataType.MultilineText)]
    public string? Notes { get; set; }

    [Display(Name = "Assigned To")]
    public string? AssignedTo { get; set; }

    public static CustomerInput From(Customer c) => new()
    {
        CustomerName = c.CustomerName,
        Email = c.Email,
        Phone = c.Phone,
        CompanyName = c.CompanyName,
        Address = c.Address,
        City = c.City,
        State = c.State,
        Status = c.Status,
        Notes = c.Notes,
        AssignedTo = c.AssignedTo
    };
}

/// <summary>Customer as returned by the REST API (no internal/audit columns).</summary>
public record CustomerDto(
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    string Email,
    string Phone,
    string? CompanyName,
    string? Address,
    string? City,
    string? State,
    CustomerStatus Status,
    string? AssignedTo,
    string? AssignedToName,
    DateTime CreatedDate)
{
    public static CustomerDto From(Customer c) => new(
        c.CustomerId, c.CustomerCode, c.CustomerName, c.Email, c.Phone, c.CompanyName,
        c.Address, c.City, c.State, c.Status, c.AssignedTo, c.AssignedUser?.FullName, c.CreatedDate);
}
