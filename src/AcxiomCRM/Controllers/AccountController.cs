using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers;

public class AccountController : CrmController
{
    private readonly AuthService _auth;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditService _audit;

    public AccountController(AuthService auth, UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager, IAuditService audit)
    {
        _auth = auth;
        _userManager = userManager;
        _signInManager = signInManager;
        _audit = audit;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var (outcome, _) = await _auth.LoginAsync(model.Email, model.Password, model.RememberMe);
        switch (outcome)
        {
            case LoginOutcome.Success:
                // Only redirect to local URLs to prevent open-redirect attacks.
                return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction("Index", "Home");
            case LoginOutcome.LockedOut:
                ModelState.AddModelError(string.Empty,
                    "This account is temporarily locked after repeated failed sign-in attempts. Try again later or contact an administrator.");
                break;
            default:
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                break;
        }
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.RegisterAsync(model.FullName, model.Email, model.Password);
        if (result.Succeeded)
        {
            Success("Welcome to AcxiomCRM! Your account has been created.");
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code.Contains("Email") || error.Code.Contains("UserName") ? nameof(model.Email) : nameof(model.Password),
                error.Description);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _auth.LogoutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code == "PasswordMismatch" ? nameof(model.CurrentPassword) : nameof(model.NewPassword), error.Description);
            await _audit.LogAsync(AuditActions.Security, "User", user.Id, new { Event = "Password change failed" }, success: false);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        await _audit.LogAsync(AuditActions.Security, "User", user.Id, new { Event = "Password changed" });
        Success("Your password has been changed.");
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
