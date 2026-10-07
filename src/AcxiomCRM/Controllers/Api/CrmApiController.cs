using AcxiomCRM.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers.Api;

/// <summary>
/// Base for REST endpoints. Authentication is the same Identity cookie as the MVC app
/// (obtain it via POST /api/auth/login). The cookie is SameSite=Strict, which is what protects
/// these JSON endpoints from cross-site request forgery, so the MVC anti-forgery token is not required.
/// </summary>
[ApiController]
[Produces("application/json")]
[IgnoreAntiforgeryToken]
public abstract class CrmApiController : ControllerBase
{
    /// <summary>Maps a failed service result to 400 / 404 / 409 with a ProblemDetails body.</summary>
    protected ActionResult Failure(ServiceResult result)
    {
        if (result.Kind == ServiceErrorKind.NotFound) return NotFound();

        foreach (var (field, message) in result.Errors)
            ModelState.AddModelError(string.IsNullOrEmpty(field) ? "general" : field, message);

        return ValidationProblem(statusCode: result.Kind == ServiceErrorKind.Conflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status400BadRequest);
    }

    protected static object Page<TIn, TOut>(PagedList<TIn> page, Func<TIn, TOut> map) => new
    {
        Items = page.Items.Select(map),
        page.Page,
        page.PageSize,
        page.TotalCount,
        page.TotalPages
    };
}
