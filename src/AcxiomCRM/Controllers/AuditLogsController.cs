using AcxiomCRM.Infrastructure;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

/// <summary>Read-only audit trail. There are deliberately no edit/delete actions.</summary>
[Authorize(Roles = Roles.Admin)]
public class AuditLogsController : CrmController
{
    private readonly IAuditService _audit;

    public AuditLogsController(IAuditService audit)
    {
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] AuditLogFilter filter)
    {
        ViewData["Entities"] = await _audit.GetEntityNamesAsync();
        ViewData["Actions"] = AuditActions.All;
        var items = await _audit.SearchAsync(filter);
        ViewData["Changes"] = await _audit.DescribeChangesAsync(items.Items);
        return View(new ListViewModel<AuditLog, AuditLogFilter> { Items = items, Filter = filter });
    }
}
