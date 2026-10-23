using FinanceOS.Application.Contracts;
using FinanceOS.Infrastructure.Data;
using FinanceOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceOS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/onboarding")]
[ServiceFilter(typeof(DomainExceptionFilter))]
public sealed class OnboardingController(FinanceOsService finance) : ControllerBase
{
    [HttpGet("templates")]
    [AllowAnonymous]
    public IActionResult Templates() => Ok(TemplateEngine.AvailableTemplates);

    [HttpPost("apply")]
    public async Task<ActionResult<AuthStatusDto>> Apply([FromBody] ApplyOnboardingRequest request, CancellationToken ct)
    {
        var owner = await finance.ApplyOnboardingAsync(request, ct);
        return Ok(new AuthStatusDto(
            false,
            true,
            owner.DisplayName,
            owner.Email,
            owner.BaseCurrency.ToString().ToUpperInvariant(),
            owner.OnboardingCompleted));
    }
}
