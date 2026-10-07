using System.Security.Claims;
using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

/// <summary>
/// Which CRM records the current user may see, per spec §7.1:
/// Admin = all, Manager = own + team (direct reports) + unassigned, Sales Executive = own only.
/// </summary>
public sealed class DataScope
{
    public bool IsUnrestricted { get; init; }
    public IReadOnlyCollection<string> UserIds { get; init; } = Array.Empty<string>();
    public bool IncludeUnassigned { get; init; }

    public bool CanAccess(string? ownerId) =>
        IsUnrestricted || (ownerId == null ? IncludeUnassigned : UserIds.Contains(ownerId));
}

public interface ICurrentUserService
{
    string UserId { get; }
    string? UserName { get; }
    bool IsAdmin { get; }
    bool IsManager { get; }
    bool IsSalesExecutive { get; }
    Task<DataScope> GetScopeAsync();
    Task<List<SelectListItem>> GetAssignableUsersAsync(string? selected = null);
}

public class CurrentUserService : ICurrentUserService
{
    private readonly ClaimsPrincipal _user;
    private readonly ApplicationDbContext _db;
    private DataScope? _scope;

    public CurrentUserService(IHttpContextAccessor accessor, ApplicationDbContext db)
    {
        _user = accessor.HttpContext?.User ?? new ClaimsPrincipal();
        _db = db;
    }

    public string UserId => _user.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? throw new InvalidOperationException("No authenticated user.");
    public string? UserName => _user.Identity?.Name;
    public bool IsAdmin => _user.IsInRole(Roles.Admin);
    public bool IsManager => _user.IsInRole(Roles.Manager);
    public bool IsSalesExecutive => _user.IsInRole(Roles.SalesExecutive);

    public async Task<DataScope> GetScopeAsync()
    {
        if (_scope != null) return _scope;

        if (IsAdmin)
            return _scope = new DataScope { IsUnrestricted = true, IncludeUnassigned = true };

        if (IsManager)
        {
            var team = await _db.Users.Where(u => u.ManagerId == UserId).Select(u => u.Id).ToListAsync();
            team.Add(UserId);
            return _scope = new DataScope { UserIds = team, IncludeUnassigned = true };
        }

        // Sales Executive (and any user without a recognised role) only sees their own records.
        return _scope = new DataScope { UserIds = new[] { UserId } };
    }

    public async Task<List<SelectListItem>> GetAssignableUsersAsync(string? selected = null)
    {
        var scope = await GetScopeAsync();
        var query = _db.Users.Where(u => u.IsActive);
        if (!scope.IsUnrestricted)
        {
            var ids = scope.UserIds;
            query = query.Where(u => ids.Contains(u.Id));
        }

        return await query
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem(u.FullName, u.Id, u.Id == selected))
            .ToListAsync();
    }
}

public static class ScopeQueryExtensions
{
    public static IQueryable<T> VisibleTo<T>(this IQueryable<T> query, DataScope scope) where T : class, IAssignable
    {
        if (scope.IsUnrestricted) return query;
        var ids = scope.UserIds;
        return scope.IncludeUnassigned
            ? query.Where(e => e.AssignedTo == null || ids.Contains(e.AssignedTo))
            : query.Where(e => e.AssignedTo != null && ids.Contains(e.AssignedTo));
    }
}
