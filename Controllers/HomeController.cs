using ANU_Admissions.Models;
using Microsoft.AspNetCore.Mvc;

namespace ANU_Admissions.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult HttpStatus(int code)
    {
        var safeStatusCode = code is >= 400 and <= 599
            ? code
            : StatusCodes.Status404NotFound;
        Response.StatusCode = safeStatusCode;
        return View(safeStatusCode);
    }
}
