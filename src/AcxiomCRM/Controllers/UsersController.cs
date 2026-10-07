using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

/// <summary>User &amp; Role Management - Admin only (spec §4.7, §7.1).</summary>
[Authorize(Roles = Infrastructure.Roles.Admin)]
public class UsersController : CrmController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public UsersController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, IAuditService audit,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _db = db;
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(string? search, string? role, bool? active)
    {
        var query = _db.Users.Include(u => u.Manager).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));
        if (active.HasValue)
            query = query.Where(u => u.IsActive == active.Value);

        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        var roleLookup = await UserRolesAsync();
        var now = DateTimeOffset.UtcNow;

        var items = users.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? string.Empty,
            Role = roleLookup.GetValueOrDefault(u.Id, "-"),
            ManagerName = u.Manager?.FullName,
            IsActive = u.IsActive,
            IsLockedOut = u.LockoutEnd > now,
            LockoutEnd = u.LockoutEnd,
            FailedLoginCount = u.AccessFailedCount,
            CreatedDate = u.CreatedDate
        }).Where(u => string.IsNullOrEmpty(role) || u.Role == role).ToList();

        return View(new UserListViewModel { Search = search, Role = role, Active = active, Users = items });
    }

    public async Task<IActionResult> Roles()
    {
        var roleLookup = await UserRolesAsync();
        var users = await _db.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        return View(Infrastructure.Roles.All.Select(r => new RoleSummaryViewModel
        {
            Role = r,
            Description = PermissionMatrix.RoleDescriptions[r],
            Members = users.Where(u => roleLookup.GetValueOrDefault(u.Id) == r)
                .Select(u => new RoleMemberViewModel(u.Id, u.FullName, u.Email ?? string.Empty, u.IsActive)).ToList()
        }).ToList());
    }

    /// <summary>What each role may do in each module - the rules enforced by [Authorize] and data scoping.</summary>
    public IActionResult Permissions() => View(PermissionMatrix.Rows);

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync(null);
        return View("Form", new UserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (string.IsNullOrEmpty(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Initial password is required.");
        ValidateRoleAndManager(model);

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                UserName = model.Email.Trim(),
                IsActive = model.IsActive,
                EmailConfirmed = true,
                ManagerId = model.Role == Infrastructure.Roles.SalesExecutive ? model.ManagerId : null
            };
            var result = await _userManager.CreateAsync(user, model.Password!);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                await _audit.LogAsync(AuditActions.Create, "User", user.Id, new { user.Email, user.FullName, model.Role, user.IsActive });
                Success($"User {user.FullName} created.");
                return RedirectToAction(nameof(Index));
            }
            AddIdentityErrors(result);
        }
        await LoadLookupsAsync(null);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        var roles = await _userManager.GetRolesAsync(user);
        await LoadLookupsAsync(user.Id);
        return View("Form", new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? Infrastructure.Roles.SalesExecutive,
            ManagerId = user.ManagerId,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        model.Id = id;

        // Passwords are not changed from this form.
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));
        ValidateRoleAndManager(model);

        var currentRoles = await _userManager.GetRolesAsync(user);
        var oldRole = currentRoles.FirstOrDefault();
        if (id == _currentUser.UserId && (!model.IsActive || model.Role != Infrastructure.Roles.Admin))
            ModelState.AddModelError(string.Empty, "You cannot deactivate or demote your own administrator account.");

        if (ModelState.IsValid)
        {
            var wasActive = user.IsActive;
            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();
            user.UserName = model.Email.Trim();
            user.IsActive = model.IsActive;
            user.ManagerId = model.Role == Infrastructure.Roles.SalesExecutive ? model.ManagerId : null;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                if (oldRole != model.Role)
                {
                    if (currentRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRoleAsync(user, model.Role);
                    await _audit.LogAsync(AuditActions.RoleChange, "User", user.Id, new { OldRole = oldRole, NewRole = model.Role });
                }
                if (wasActive != model.IsActive)
                {
                    await _audit.LogAsync(AuditActions.Security, "User", user.Id,
                        new { Event = model.IsActive ? "User activated" : "User deactivated" });
                }
                // Invalidate existing sessions so role/status changes apply immediately.
                if (oldRole != model.Role || wasActive != model.IsActive)
                    await _userManager.UpdateSecurityStampAsync(user);

                await _audit.LogAsync(AuditActions.Update, "User", user.Id, new { user.Email, user.FullName, user.ManagerId });
                Success("User updated.");
                return RedirectToAction(nameof(Index));
            }
            AddIdentityErrors(result);
        }
        await LoadLookupsAsync(id);
        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _audit.LogAsync(AuditActions.Security, "User", user.Id, new { Event = "Account unlocked by administrator" });
        Success($"{user.FullName} has been unlocked.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();
        return View(new ResetPasswordViewModel { Id = id, FullName = user.FullName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null) return NotFound();
        model.FullName = user.FullName;
        if (!ModelState.IsValid) return View(model);

        // Token-based reset runs the full password policy and rotates the security stamp.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(nameof(model.NewPassword), e.Description);
            return View(model);
        }
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _audit.LogAsync(AuditActions.Security, "User", user.Id, new { Event = "Password reset by administrator" });
        Success($"Password reset for {user.FullName}.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidateRoleAndManager(UserFormViewModel model)
    {
        if (!Infrastructure.Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Select a valid role.");
    }

    private async Task LoadLookupsAsync(string? excludeUserId)
    {
        var managers = await _userManager.GetUsersInRoleAsync(Infrastructure.Roles.Manager);
        ViewData["Managers"] = managers.Where(m => m.Id != excludeUserId && m.IsActive).OrderBy(m => m.FullName)
            .Select(m => new SelectListItem(m.FullName, m.Id)).ToList();
        ViewData["Roles"] = Infrastructure.Roles.All.Select(r => new SelectListItem(Infrastructure.Roles.DisplayName(r), r)).ToList();
    }

    private async Task<Dictionary<string, string>> UserRolesAsync() =>
        await (from ur in _db.UserRoles
               join r in _db.Roles on ur.RoleId equals r.Id
               select new { ur.UserId, r.Name })
            .ToDictionaryAsync(x => x.UserId, x => x.Name!);

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var e in result.Errors)
        {
            var key = e.Code.Contains("Email") || e.Code.Contains("UserName") ? "Email"
                : e.Code.StartsWith("Password") ? "Password" : string.Empty;
            ModelState.AddModelError(key, e.Description);
        }
    }
}
