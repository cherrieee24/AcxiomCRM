using AcxiomCRM.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public abstract class CrmController : Controller
{
    protected void AddErrors(ServiceResult result)
    {
        foreach (var (field, message) in result.Errors)
            ModelState.AddModelError(field, message);
    }

    protected void Success(string message) => TempData["Success"] = message;
    protected void Error(string message) => TempData["Error"] = message;

    /// <summary>Maps a failed service result for actions that redirect rather than re-render a form.</summary>
    protected IActionResult RedirectWithResult(ServiceResult result, string successMessage, string action, object? routeValues = null)
    {
        if (result.Kind == ServiceErrorKind.NotFound) return NotFound();
        if (result.Succeeded) Success(successMessage);
        else Error(string.Join(" ", result.Errors.Values));
        return RedirectToAction(action, routeValues);
    }
}
