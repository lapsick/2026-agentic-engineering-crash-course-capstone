using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using ToolShare.Membership.Blazor.Security;

namespace ToolShare.Blazor.Security;

/// <summary>
/// Decorates ASP.NET Core's default <see cref="IAuthorizationMiddlewareResultHandler"/> so an
/// authenticated-but-not-enrolled (or deactivated) visitor denied by
/// <see cref="MembershipActiveMemberRequirement"/> lands on the explanatory
/// <c>/membership/not-enrolled</c> page (research R3) instead of the generic
/// ABP "Access denied" page every other authorization failure (e.g. a real
/// permission-based policy) still gets. The handler flags the denial via
/// <see cref="HttpContext.Items"/> (<see cref="MembershipActiveMemberRequirement.DeniedByEnrolmentGateItemKey"/>)
/// since <see cref="PolicyAuthorizationResult"/> doesn't otherwise say which
/// requirement failed.
/// </summary>
public class NotEnrolledRedirectAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly IAuthorizationMiddlewareResultHandler _defaultHandler = new AuthorizationMiddlewareResultHandler();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Succeeded
            && context.Items.ContainsKey(MembershipActiveMemberRequirement.DeniedByEnrolmentGateItemKey))
        {
            context.Response.Redirect(MembershipActiveMemberRequirementHandler.NotEnrolledPagePath);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
