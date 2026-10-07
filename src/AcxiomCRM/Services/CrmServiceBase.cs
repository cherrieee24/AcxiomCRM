using System.ComponentModel.DataAnnotations;
using AcxiomCRM.Data;
using AcxiomCRM.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Services;

public abstract class CrmServiceBase
{
    protected readonly ApplicationDbContext Db;
    protected readonly ICurrentUserService CurrentUser;

    protected CrmServiceBase(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        Db = db;
        CurrentUser = currentUser;
    }

    /// <summary>
    /// Re-runs data-annotation validation on the server, so rules hold even when a service is
    /// called from code paths that bypass MVC/API model binding.
    /// </summary>
    protected static ServiceResult ValidateInput(object input)
    {
        var results = new List<ValidationResult>();
        var result = ServiceResult.Ok();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true))
        {
            foreach (var r in results)
                result.AddError(r.MemberNames.FirstOrDefault() ?? string.Empty, r.ErrorMessage ?? "Invalid value.");
        }
        return result;
    }

    /// <summary>
    /// Sales Executives always own what they create; Admins/Managers may assign to any active
    /// user inside their scope (or leave a record unassigned).
    /// </summary>
    protected async Task<(string? AssignedTo, ServiceResult Result)> ResolveAssigneeAsync(string? requested)
    {
        if (CurrentUser.IsSalesExecutive && !CurrentUser.IsAdmin && !CurrentUser.IsManager)
            return (CurrentUser.UserId, ServiceResult.Ok());

        if (string.IsNullOrWhiteSpace(requested))
            return (null, ServiceResult.Ok());

        var scope = await CurrentUser.GetScopeAsync();
        var exists = await Db.Users.AnyAsync(u => u.Id == requested && u.IsActive);
        if (!exists || !scope.CanAccess(requested))
            return (null, ServiceResult.Invalid("AssignedTo", "Select a valid user to assign."));

        return (requested, ServiceResult.Ok());
    }

    protected static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
