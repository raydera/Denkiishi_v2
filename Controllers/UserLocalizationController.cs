using Denkiishi_v2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Denkiishi_v2.Controllers;

[ApiController]
[Route("api/user/localization")]
[Authorize]
public class UserLocalizationController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserLocalizationController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public sealed class UpdateLocalizationRequest
    {
        public string? TimeZone { get; set; }
        public string? Country { get; set; }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateLocalizationRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var tz = (request.TimeZone ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(tz) && !string.Equals(user.TimeZone, tz, StringComparison.Ordinal))
        {
            user.TimeZone = tz;
        }

        var country = (request.Country ?? "").Trim();
        if (string.IsNullOrWhiteSpace(country))
        {
            // país é opcional; não sobrescreve com vazio
        }
        else if (!string.Equals(user.Country, country, StringComparison.OrdinalIgnoreCase))
        {
            user.Country = country;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Problem("Não foi possível atualizar a localização do usuário.");
        }

        return Ok(new { user.TimeZone, user.Country });
    }
}

