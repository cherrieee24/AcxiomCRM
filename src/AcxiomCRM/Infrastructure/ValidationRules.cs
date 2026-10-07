namespace AcxiomCRM.Infrastructure;

/// <summary>Single source of truth for validation patterns and messages (spec §5.4).</summary>
public static class ValidationRules
{
    /// <summary>10-digit Indian mobile number starting with 6-9.</summary>
    public const string PhonePattern = @"^[6-9]\d{9}$";

    /// <summary>Stricter than [EmailAddress]: requires local@domain.tld with no spaces.</summary>
    public const string EmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$";

    public const string PhoneMessage = "Enter a valid phone number.";
    public const string EmailMessage = "Enter a valid email address.";
    public const string AmountMessage = "Opportunity Amount must be greater than 0.";
    public const string ProbabilityMessage = "Probability must be between 0 and 100.";
    public const string CloseDateMessage = "Expected Close Date cannot be in the past.";
    public const string FollowUpDateMessage = "Follow-up date cannot be earlier than today.";
    public const string LengthMessage = "{0} cannot exceed {1} characters.";

    public const double MaxAmount = 1_000_000_000;
    public const double MaxLeadValue = 100_000_000;
}
