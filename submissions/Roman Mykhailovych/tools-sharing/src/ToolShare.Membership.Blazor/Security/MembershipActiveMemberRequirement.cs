using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using ToolShare.Membership.Members;
using Volo.Abp.Security.Claims;

namespace ToolShare.Membership.Blazor.Security;

public class MembershipActiveMemberRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// <see cref="HttpContext.Items"/> key the handler sets to <c>true</c> when it
    /// denies a request (authenticated but not an active member), so
    /// <c>NotEnrolledRedirectAuthorizationMiddlewareResultHandler</c> (host level —
    /// ToolShare.Blazor) can tell "denied by this specific requirement" apart from
    /// any other authorization failure (e.g. a real permission-based policy) and
    /// redirect to the explanatory page instead of the generic AccessDenied page.
    /// </summary>
    public const string DeniedByEnrolmentGateItemKey = "ToolShare.Membership.DeniedByEnrolmentGate";
}

/// <summary>
/// UI affordance half of the enrolment gate (research R3): added to the Blazor
/// router's fallback policy alongside
/// <c>RequireAuthenticationExceptKnownAnonymousPathsRequirement</c> so an
/// authenticated-but-not-enrolled (or deactivated) visitor is redirected to the
/// explanatory page instead of hitting a raw <see cref="Volo.Abp.Authorization.AbpAuthorizationException"/>
/// dialog. This is <b>not</b> the authoritative check — that is
/// <c>MembershipMethodInvocationAuthorizationService</c>, which covers every
/// application-service call regardless of whether a Blazor route was even
/// involved. This handler only governs page navigation.
/// </summary>
public class MembershipActiveMemberRequirementHandler : AuthorizationHandler<MembershipActiveMemberRequirement>
{
    /// <summary>
    /// Paths that must stay reachable even for an authenticated non-member — the
    /// same anonymous-infrastructure allowlist
    /// <c>RequireAuthenticationExceptKnownAnonymousPathsHandler</c> uses, plus the
    /// explanatory page itself, so the redirect target can never loop.
    /// </summary>
    private static readonly string[] ExemptPathPrefixes =
    {
        "/Account",
        "/swagger",
        "/connect",
        "/.well-known",
        "/_blazor",
        "/_framework",
        "/_content",
        "/_vs",
        "/Abp",
        NotEnrolledPagePath
    };

    /// <summary>The explanatory page's actual route (<c>NotEnrolled.razor</c>'s <c>@page</c> directive) — also the redirect target used by the host's result handler.</summary>
    public const string NotEnrolledPagePath = "/membership/not-enrolled";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemberStandingProvider _memberStandingProvider;

    public MembershipActiveMemberRequirementHandler(IHttpContextAccessor httpContextAccessor, IMemberStandingProvider memberStandingProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _memberStandingProvider = memberStandingProvider;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MembershipActiveMemberRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            // Not this handler's concern — RequireAuthenticationExceptKnownAnonymousPathsRequirement
            // governs unauthenticated visitors; failing here too would just
            // produce a confusing double-denial on the same navigation.
            context.Succeed(requirement);
            return;
        }

        var path = _httpContextAccessor.HttpContext?.Request.Path.Value;
        if (path != null && ExemptPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = context.User.FindFirst(AbpClaimTypes.UserId)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var identityUserId))
        {
            context.Succeed(requirement);
            return;
        }

        var standing = await _memberStandingProvider.GetByIdentityUserIdAsync(identityUserId);
        if (standing is { IsActive: true })
        {
            context.Succeed(requirement);
            return;
        }

        // Otherwise: neither Succeed nor Fail — leaves the requirement
        // unsatisfied, which denies the request the same way the existing
        // handler does for an unrecognized anonymous path. Flag it so the
        // host's result handler can redirect to the explanatory page instead
        // of the generic AccessDenied page.
        if (_httpContextAccessor.HttpContext is { } httpContext)
        {
            httpContext.Items[MembershipActiveMemberRequirement.DeniedByEnrolmentGateItemKey] = true;
        }
    }
}
