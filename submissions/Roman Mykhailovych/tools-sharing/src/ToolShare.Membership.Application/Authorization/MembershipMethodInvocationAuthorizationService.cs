using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using ToolShare.Membership.Authorization;
using ToolShare.Membership.Localization;
using ToolShare.Membership.Members;
using Volo.Abp.Authorization;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;

namespace ToolShare.Membership.Authorization;

/// <summary>
/// The enrolment gate (FR-006, FR-006a; research R3): decorates ABP's
/// <see cref="IMethodInvocationAuthorizationService"/>, the single seam
/// <c>AuthorizationInterceptor</c> routes every application-service call
/// through — a bare <c>[Authorize]</c>, an <c>[Authorize(SomePermission)]</c>,
/// or no attribute at all. After delegating to the wrapped (normal ABP)
/// authorization check — which still enforces every existing
/// <c>[Authorize(Permission)]</c> — this additionally refuses the call unless
/// the caller resolves to an enrolled, <c>Active</c> member, regardless of what
/// permissions they hold. <c>[AllowAnonymous]</c>-attributed methods are
/// delegated untouched, exactly like the wrapped service's own handling, so the
/// login/account flow and the forced first-sign-in password change (FR-001a)
/// keep working.
///
/// Thrown as <see cref="AbpAuthorizationException"/> — not a
/// <c>BusinessException</c> — deliberately: this is fundamentally an
/// authorization decision, and every existing SC-003-style assertion in this
/// codebase (<c>BrowseOnlyUserTests</c>, <c>AnonymousAccessTests</c>) already
/// expects that type, so the gate stays consistent with the rest of the
/// authorization surface a caller has to handle.
/// </summary>
public class MembershipMethodInvocationAuthorizationService : IMethodInvocationAuthorizationService
{
    private readonly IMethodInvocationAuthorizationService _inner;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IMemberStandingProvider _memberStandingProvider;
    private readonly IStringLocalizer<MembershipResource> _localizer;

    public MembershipMethodInvocationAuthorizationService(
        IMethodInvocationAuthorizationService inner,
        ICurrentUser currentUser,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IMemberStandingProvider memberStandingProvider,
        IStringLocalizer<MembershipResource> localizer)
    {
        _inner = inner;
        _currentUser = currentUser;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _memberStandingProvider = memberStandingProvider;
        _localizer = localizer;
    }

    public virtual async Task CheckAsync(MethodInvocationAuthorizationContext context)
    {
        // Preserves every existing [Authorize]/[Authorize(Permission)] check
        // (and [AllowAnonymous]'s own short-circuit) exactly as ABP's own
        // MethodInvocationAuthorizationService would have.
        await _inner.CheckAsync(context);

        if (IsAllowAnonymous(context))
        {
            return;
        }

        // 004-lending's background workers run with no HTTP request, no
        // signed-in user, and therefore no ambient member standing at all —
        // an explicit, code-level opt-out (never satisfiable by a real user's
        // principal) rather than an implicit environmental heuristic. See
        // SystemPrincipal's own remarks for why "no HttpContext" was rejected
        // as the detection mechanism.
        if (SystemPrincipal.IsSystemPrincipal(_currentPrincipalAccessor.Principal))
        {
            return;
        }

        if (!_currentUser.IsAuthenticated || _currentUser.Id is not { } identityUserId)
        {
            // A method with no [Authorize] attribute at all would otherwise sail
            // through the inner check above with no policy to evaluate — the
            // gate must refuse it independently (FR-006: "regardless of what
            // permissions they hold").
            throw new AbpAuthorizationException(_localizer[MembershipDomainErrorCodes.NotAnEnrolledMember])
                .WithData("Reason", MembershipDomainErrorCodes.NotAnEnrolledMember);
        }

        var standing = await _memberStandingProvider.GetByIdentityUserIdAsync(identityUserId);
        if (standing is null)
        {
            throw new AbpAuthorizationException(_localizer[MembershipDomainErrorCodes.NotAnEnrolledMember])
                .WithData("Reason", MembershipDomainErrorCodes.NotAnEnrolledMember);
        }

        if (!standing.IsActive)
        {
            throw new AbpAuthorizationException(_localizer[MembershipDomainErrorCodes.MembershipInactive])
                .WithData("Reason", MembershipDomainErrorCodes.MembershipInactive);
        }
    }

    private static bool IsAllowAnonymous(MethodInvocationAuthorizationContext context)
    {
        return context.Method.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any();
    }
}
