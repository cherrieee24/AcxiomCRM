using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

[Route("api/auth")]
public class AuthApiController : CrmApiController
{
    private readonly AuthService _auth;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthApiController(AuthService auth, UserManager<ApplicationUser> userManager)
    {
        _auth = auth;
        _userManager = userManager;
    }

    public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

    /// <summary>Signs in and issues the auth cookie. Rate limited; failed attempts count towards lockout.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var (outcome, user) = await _auth.LoginAsync(request.Email, request.Password, rememberMe: false);
        return outcome switch
        {
            LoginOutcome.Success => Ok(new
            {
                user!.Id,
                user.FullName,
                user.Email,
                Roles = await _userManager.GetRolesAsync(user)
            }),
            LoginOutcome.LockedOut => Problem("Account is temporarily locked.", statusCode: StatusCodes.Status423Locked),
            _ => Problem("Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized)
        };
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await _auth.LogoutAsync();
        return NoContent();
    }
}
