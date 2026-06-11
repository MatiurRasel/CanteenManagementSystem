using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers;

public class CultureController : Controller
{
    [HttpGet]
    public IActionResult Set(string culture, string returnUrl = "/")
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            return LocalRedirect(returnUrl);
        }

        var requestCulture = new RequestCulture(culture);
        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(requestCulture);

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            cookieValue,
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

        return LocalRedirect(returnUrl);
    }
}
