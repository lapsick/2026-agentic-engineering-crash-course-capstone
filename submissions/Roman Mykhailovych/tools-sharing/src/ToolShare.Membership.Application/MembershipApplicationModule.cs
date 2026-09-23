using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using ToolShare.Membership.Authorization;
using ToolShare.Membership.Localization;
using ToolShare.Membership.Members;
using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;

namespace ToolShare.Membership;

[DependsOn(
    typeof(MembershipDomainModule),
    typeof(MembershipApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpCachingModule)
    )]
public class MembershipApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // The enrolment gate (research R3, T061/T062): decorates the single seam
        // every application-service call passes through, regardless of which
        // module defines it — so registering it here, once, covers Membership's
        // own app services AND every other module's (e.g. Catalog's) in the same
        // host/test process, without those modules needing to know Membership
        // exists. Constructs the wrapped ABP service directly rather than relying
        // on it also being registered under its own concrete type, since that is
        // an implementation detail of ABP's conventional registration this
        // module should not depend on.
        context.Services.Replace(ServiceDescriptor.Transient<IMethodInvocationAuthorizationService>(sp =>
            new MembershipMethodInvocationAuthorizationService(
                new MethodInvocationAuthorizationService(
                    sp.GetRequiredService<IAbpAuthorizationPolicyProvider>(),
                    sp.GetRequiredService<IAbpAuthorizationService>()),
                sp.GetRequiredService<ICurrentUser>(),
                sp.GetRequiredService<ICurrentPrincipalAccessor>(),
                sp.GetRequiredService<IMemberStandingProvider>(),
                sp.GetRequiredService<IStringLocalizer<MembershipResource>>())));
    }
}
