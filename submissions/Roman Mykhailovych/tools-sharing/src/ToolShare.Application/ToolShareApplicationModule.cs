using ToolShare.Catalog;
using ToolShare.Membership;
using ToolShare.Notifications;
using Volo.Abp.Account;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;
using Microsoft.Extensions.DependencyInjection;

namespace ToolShare;

[DependsOn(
    typeof(ToolShareDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(ToolShareApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    // Needed for LibrarianRoleDataSeedContributor, which grants CatalogPermissions
    // to the Librarian role. Referencing the Catalog module's Application.Contracts
    // (never its Domain/EntityFrameworkCore) is the allowed cross-module coupling.
    typeof(CatalogApplicationContractsModule),
    // Same allowed coupling for MembershipPermissions (role seeding, T057) and
    // IMemberIdentityProvisioner (IdentityMemberIdentityProvisioner, T056) — the
    // host is permitted to reference another module's Application.Contracts,
    // never its Domain/EntityFrameworkCore (research R2).
    typeof(MembershipApplicationContractsModule),
    // Needed for NotificationsPermissions.Audit, granted to Administrator by
    // MembershipRoleDataSeedContributor (005-notifications) — without this,
    // NotificationsPermissionDefinitionProvider is never discovered by the
    // host's module tree and the permission the grant references doesn't exist.
    typeof(NotificationsApplicationContractsModule)
    )]
public class ToolShareApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<ToolShareApplicationModule>();
    }
}
