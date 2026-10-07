using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Identity;

namespace AcxiomCRM.Services;

public enum LoginOutcome
{
    Success,
    Invalid,
    LockedOut
}

/// <summary>Password sign-in shared by the MVC login page and the REST API, with lockout and auditing.</summary>
public class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _audit;

    public AuthService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    public async Task<(LoginOutcome Outcome, ApplicationUser? User)> LoginAsync(string email, string password, bool rememberMe)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
        {
            // Audit the attempt without revealing (to the caller) whether the account exists.
            await _audit.LogAsync(AuditActions.LoginFailed, "Authentication", null, new { Reason = "Unknown user" },
                success: false, userName: email.Trim());
            return (LoginOutcome.Invalid, null);
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _audit.LogAsync(AuditActions.Login, "Authentication", user.Id, null, userId: user.Id, userName: user.UserName);
            return (LoginOutcome.Success, user);
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync(AuditActions.Lockout, "Authentication", user.Id,
                new { Reason = "Account locked after repeated failed attempts", user.LockoutEnd },
                success: false, userId: user.Id, userName: user.UserName);
            return (LoginOutcome.LockedOut, user);
        }

        var reason = result.IsNotAllowed ? "Account inactive" : "Invalid password";
        await _audit.LogAsync(AuditActions.LoginFailed, "Authentication", user.Id,
            new { Reason = reason, FailedAttempts = await _userManager.GetAccessFailedCountAsync(user) },
            success: false, userId: user.Id, userName: user.UserName);
        return (LoginOutcome.Invalid, user);
    }

    public async Task LogoutAsync()
    {
        await _audit.LogAsync(AuditActions.Logout, "Authentication");
        await _signInManager.SignOutAsync();
    }

    public async Task<IdentityResult> RegisterAsync(string fullName, string email, string password)
    {
        var user = new ApplicationUser { FullName = fullName.Trim(), Email = email.Trim(), UserName = email.Trim(), IsActive = true };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded) return result;

        // Self-registered users get the least-privileged role; an Admin can promote them.
        await _userManager.AddToRoleAsync(user, Roles.SalesExecutive);
        await _audit.LogAsync(AuditActions.Register, "User", user.Id, new { user.Email, Role = Roles.SalesExecutive },
            userId: user.Id, userName: user.UserName);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return result;
    }
}
