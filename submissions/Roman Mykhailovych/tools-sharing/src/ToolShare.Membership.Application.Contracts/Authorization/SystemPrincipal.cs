using System.Security.Claims;

namespace ToolShare.Membership.Authorization;

/// <summary>
/// A well-known, explicit way for trusted internal system code — periodic
/// background workers, which run with no HTTP request, no signed-in user, and
/// therefore no ambient enrolled-member standing at all — to make a read-only
/// call through Membership's published contracts without being refused by the
/// enrolment gate (<c>MembershipMethodInvocationAuthorizationService</c>,
/// research R3). Deliberately an explicit, code-level opt-out (the caller must
/// construct this exact claim on purpose) rather than an implicit
/// environmental heuristic (e.g. "no HttpContext") — the latter would also
/// match a Blazor Interactive Server circuit mid-render, silently disabling
/// the gate for real user requests. No real user principal ever carries this
/// claim.
/// <para>
/// Added for 004-lending: its background workers (<c>WaitlistOfferExpiryWorker</c>,
/// <c>ReturnReminderWorker</c>) need to read Membership's community rules on a
/// schedule, with no user in the loop. This is the second (and, so far, only
/// other) production change 004 makes to an already-shipped module beyond
/// Catalog's — narrowly scoped to the gate's own exemption check, adding no
/// new public operation and weakening no existing one.
/// </para>
/// </summary>
public static class SystemPrincipal
{
    public const string ClaimType = "https://schemas.toolshare.local/system-worker";
    public const string ClaimValue = "true";

    /// <summary>
    /// A principal recognized by the enrolment gate as a trusted system
    /// caller — never authenticated as any real member. Must pass a non-empty
    /// authentication type so <c>ClaimsIdentity.IsAuthenticated</c> is
    /// <c>true</c> (the base ABP authorization check, run before the gate's
    /// own exemption logic, requires authentication for a bare
    /// <c>[Authorize]</c> attribute) — a <see cref="ClaimsIdentity"/>
    /// constructed without one is never considered authenticated regardless
    /// of the claims it carries.
    /// </summary>
    public static ClaimsPrincipal Build()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimType, ClaimValue) }, "System"));
    }

    public static bool IsSystemPrincipal(ClaimsPrincipal principal)
    {
        return principal.HasClaim(ClaimType, ClaimValue);
    }
}
