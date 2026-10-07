using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AcxiomCRM.Infrastructure;

/// <summary>
/// Rejects dates earlier than today. Optionally skipped when another property holds one of the
/// given values (e.g. an opportunity that is already Won/Lost may keep a historical close date).
/// Validates on the server and emits unobtrusive attributes for the matching rule in site.js.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotInPastAttribute : ValidationAttribute, IClientModelValidator
{
    public NotInPastAttribute() : base("{0} cannot be in the past.") { }

    /// <summary>Name of a sibling property that can switch the rule off.</summary>
    public string? UnlessProperty { get; init; }

    /// <summary>Comma-separated values of <see cref="UnlessProperty"/> that switch the rule off.</summary>
    public string? UnlessValues { get; init; }

    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateTime date) return ValidationResult.Success;

        if (UnlessProperty != null && UnlessValues != null)
        {
            var other = context.ObjectType.GetProperty(UnlessProperty)?.GetValue(context.ObjectInstance)?.ToString();
            if (other != null && UnlessValues.Split(',').Contains(other)) return ValidationResult.Success;
        }

        return date.Date >= DateTime.Today
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(context.DisplayName), new[] { context.MemberName! });
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        var attrs = context.Attributes;
        attrs.TryAdd("data-val", "true");
        attrs.TryAdd("data-val-notpast", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
        if (UnlessProperty != null && UnlessValues != null)
        {
            attrs.TryAdd("data-val-notpast-other", UnlessProperty);
            attrs.TryAdd("data-val-notpast-values", UnlessValues);
        }
    }
}
