using ToolShare.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace ToolShare.Membership;

/// <summary>
/// A throwaway ABP application used exactly once per test assembly run, purely
/// to seed the TEMPLATE database (see <see cref="MembershipApplicationTestFixture"/>)
/// with the same admin/permission bootstrap that <c>ToolShare.DbMigrator</c>
/// produces in production, plus Membership's own seed pipeline (the
/// <c>CommunityRules</c> singleton via <c>MembershipDataSeedContributor</c>,
/// auto-discovered because this module depends on
/// <see cref="MembershipApplicationModule"/>). Every per-test database is then
/// cloned from this already-seeded template. Mirrors
/// <c>CatalogAuthorizationSeedModule</c> exactly, including deliberately not
/// overriding individual DbContexts' connection strings — the caller supplies
/// the connection string via configuration instead (see
/// <see cref="MembershipApplicationTestFixture"/>).
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ToolShareApplicationModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule)
    )]
public class MembershipAuthorizationSeedModule : AbpModule
{
}
