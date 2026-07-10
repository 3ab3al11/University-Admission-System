using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ANU_Admissions.Controllers;

/// <summary>
/// Tiny endpoint the language switcher hits. Writes the ASP.NET culture
/// cookie (.AspNetCore.Culture) and bounces the user back to where they were.
/// No DB write, no auth required.
/// </summary>
public class LanguageController : Controller
{
    [HttpGet]
    public IActionResult Set(string culture, string? returnUrl = null)
    {
        if (culture is not ("ar" or "en"))
        {
            culture = "ar";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

        // Only follow a same-host returnUrl — never an absolute URL.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
