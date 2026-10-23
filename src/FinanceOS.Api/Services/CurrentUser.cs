using System.Security.Claims;
using FinanceOS.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace FinanceOS.Api.Services;

public sealed class CurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var idClaim = http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var guid) ? guid : null;
        }
    }

    public bool IsAuthenticated => http.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string? Email => http.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;

    public string? DisplayName => http.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
}
