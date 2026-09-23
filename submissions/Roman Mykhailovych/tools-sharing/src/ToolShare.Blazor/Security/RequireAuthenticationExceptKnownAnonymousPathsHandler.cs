using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace ToolShare.Blazor.Security;

public class RequireAuthenticationExceptKnownAnonymousPathsRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Backs the app-wide fallback authorization policy (FR-011): every route —
/// Catalog and otherwise — requires an authenticated user, EXCEPT the
/// infrastructure the login flow itself depends on (the ABP Account pages,
/// OpenIddict endpoints, Swagger, Blazor's own static/framework assets), which
/// must stay reachable anonymously or nobody could ever reach the login page
/// in the first place. A path-prefix allowlist is used (rather than
/// per-endpoint <c>[AllowAnonymous]</c>) because those endpoints are mapped
/// internally by ABP/Swashbuckle/OpenIddict library modules this host doesn't
/// map itself.
/// </summary>
public class RequireAuthenticationExceptKnownAnonymousPathsHandler
    : AuthorizationHandler<RequireAuthenticationExceptKnownAnonymousPathsRequirement>
{
    private static readonly string[] AnonymousPathPrefixes =
    {
        "/Account",
        "/swagger",
        "/connect",
        "/.well-known",
        "/_blazor",
        "/_framework",
        "/_content",
        "/_vs",
        "/Abp"
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequireAuthenticationExceptKnownAnonymousPathsHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequireAuthenticationExceptKnownAnonymousPathsRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var path = _httpContextAccessor.HttpContext?.Request.Path.Value;
        if (path != null && AnonymousPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
