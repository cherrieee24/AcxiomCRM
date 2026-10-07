using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcxiomCRM.Controllers;

public class HomeController : Controller
{
    private readonly DashboardService _dashboard;

    public HomeController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Role-scoped dashboard - the landing page after login.</summary>
    public async Task<IActionResult> Index(string? range, DateTime? from, DateTime? to)
    {
        return View(await _dashboard.BuildAsync(range, from, to));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [AllowAnonymous]
    [Route("Home/StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        ViewData["Code"] = code;
        return View("StatusCode");
    }
}
