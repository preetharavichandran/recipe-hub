using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RecipeHub.Application.Abstractions;

namespace RecipeHub.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Authentication";

    /// <summary>Google | Development</summary>
    public string Mode { get; set; } = "Development";
    public string GoogleAuthority { get; set; } = "https://accounts.google.com";
    public string? GoogleAudience { get; set; }
    public string DevelopmentSigningKey { get; set; } = "RecipeHub-Dev-Signing-Key-At-Least-32-Chars!";
    public string DevelopmentIssuer { get; set; } = "recipehub-dev";
    public string DevelopmentAudience { get; set; } = "recipehub";
    public List<string> AdminSubs { get; set; } = [];
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor, IOptions<AuthOptions> options) : ICurrentUser
{
    public string? CreatorId =>
        accessor.HttpContext?.User?.FindFirstValue("sub")
        ?? accessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAuthenticated =>
        accessor.HttpContext?.User?.Identity?.IsAuthenticated == true
        && !string.IsNullOrWhiteSpace(CreatorId);

    public bool IsAdmin
    {
        get
        {
            if (!IsAuthenticated || CreatorId is null)
                return false;
            return options.Value.AdminSubs.Contains(CreatorId, StringComparer.Ordinal);
        }
    }
}
