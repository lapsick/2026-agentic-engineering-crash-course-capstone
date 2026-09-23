using System;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using ToolShare.Blazor.Components;
using ToolShare.Blazor.Menus;
using ToolShare.Blazor.Security;
using ToolShare.Catalog;
using ToolShare.Catalog.Blazor;
using ToolShare.EntityFrameworkCore;
using ToolShare.Localization;
using ToolShare.Lending;
using ToolShare.Lending.Blazor;
using ToolShare.Membership;
using ToolShare.Membership.Blazor;
using ToolShare.Membership.Blazor.Security;
using ToolShare.Notifications;
using ToolShare.Notifications.Blazor;
using ToolShare.MultiTenancy;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Server.MudBlazorBasicTheme;
using Volo.Abp.AspNetCore.Components.Server.MudBlazorBasicTheme.Bundling;
using Volo.Abp.AspNetCore.Components.Web.Theming.MudBlazor.Routing;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.Localization;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Mapperly;
using Volo.Abp.Identity.Blazor.MudBlazor.Server;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement.Blazor.MudBlazor.Server;
using Volo.Abp.Swashbuckle;
using Volo.Abp.TenantManagement.Blazor.MudBlazor.Server;
using Volo.Abp.UI;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace ToolShare.Blazor;

[DependsOn(
    typeof(ToolShareApplicationModule),
    typeof(ToolShareEntityFrameworkCoreModule),
    typeof(ToolShareHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreComponentsServerMudBlazorBasicThemeModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpIdentityBlazorMudBlazorServerModule),
    typeof(AbpTenantManagementBlazorMudBlazorServerModule),
    typeof(AbpSettingManagementBlazorMudBlazorServerModule),
    typeof(CatalogApplicationModule),
    typeof(CatalogEntityFrameworkCoreModule),
    typeof(CatalogBlazorModule),
    typeof(MembershipApplicationModule),
    typeof(MembershipEntityFrameworkCoreModule),
    typeof(MembershipBlazorModule),
    typeof(LendingApplicationModule),
    typeof(LendingEntityFrameworkCoreModule),
    typeof(LendingBlazorModule),
    typeof(NotificationsApplicationModule),
    typeof(NotificationsEntityFrameworkCoreModule),
    typeof(NotificationsBlazorModule)
   )]
public class ToolShareBlazorModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        context.Services.PreConfigure<AbpMvcDataAnnotationsLocalizationOptions>(options =>
        {
            options.AddAssemblyResource(
                typeof(ToolShareResource),
                typeof(ToolShareDomainModule).Assembly,
                typeof(ToolShareDomainSharedModule).Assembly,
                typeof(ToolShareApplicationModule).Assembly,
                typeof(ToolShareApplicationContractsModule).Assembly,
                typeof(ToolShareBlazorModule).Assembly
            );
        });

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("ToolShare");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", "c987672a-6212-43bf-922d-73e0c4fa15a6");
            });
        }

        PreConfigure<AbpAspNetCoreComponentsWebOptions>(options =>
        {
            options.IsBlazorWebApp = true;
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        // Add services to the container.
        context.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        ConfigureAuthentication(context);
        ConfigureAuthorization(context);
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureVirtualFileSystem(hostingEnvironment);
        ConfigureSwaggerServices(context.Services);
        ConfigureAutoApiControllers();
        ConfigureRouter(context);
        ConfigureMenu(context);

        context.Services.AddMapperlyObjectMapper<ToolShareBlazorModule>();
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    /// <summary>
    /// FR-011: every route (Catalog and otherwise) requires an authenticated user.
    /// There is no anonymous Catalog page and no <c>[AllowAnonymous]</c> anywhere
    /// in the Catalog module, so this fallback policy is what actually turns an
    /// anonymous visit into a redirect to the login page (see
    /// contracts/catalog-permissions.md). It's enforced by
    /// <see cref="RequireAuthenticationExceptKnownAnonymousPathsHandler"/> rather
    /// than a plain <c>RequireAuthenticatedUser()</c> policy, since a blanket
    /// fallback also intercepts the ABP Account pages, Swagger and OpenIddict's
    /// own endpoints — none of which carry explicit <c>[AllowAnonymous]</c>
    /// metadata (they're mapped inside those library modules, not here) — which
    /// would otherwise lock out the login page itself.
    ///
    /// Since 003 (research R3), the same fallback also requires
    /// <see cref="MembershipActiveMemberRequirement"/>: page navigation by an
    /// authenticated-but-not-enrolled (or deactivated) visitor is redirected to
    /// the explanatory page rather than shown a raw authorization exception. This
    /// is the UI-affordance half of the enrolment gate — the authoritative half
    /// (FR-006, FR-006a) is <c>MembershipMethodInvocationAuthorizationService</c>,
    /// which governs every application-service call regardless of the route.
    /// The same combined policy is also exposed under the name
    /// <see cref="MembershipActiveMemberPolicyName"/> for any component that wants
    /// to check it explicitly (e.g. via <c>AuthorizeView Policy="..."</c>).
    /// </summary>
    private void ConfigureAuthorization(ServiceConfigurationContext context)
    {
        context.Services.AddHttpContextAccessor();
        context.Services.AddTransient<IAuthorizationHandler, RequireAuthenticationExceptKnownAnonymousPathsHandler>();

        Configure<AuthorizationOptions>(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddRequirements(
                    new RequireAuthenticationExceptKnownAnonymousPathsRequirement(),
                    new MembershipActiveMemberRequirement())
                .Build();

            options.AddPolicy(MembershipActiveMemberPolicyName, policyBuilder =>
                policyBuilder.AddRequirements(new MembershipActiveMemberRequirement()));
        });
    }

    /// <summary>The named ASP.NET Core authorization policy backing the enrolment gate's UI affordance (research R3, T062).</summary>
    public const string MembershipActiveMemberPolicyName = "Membership.ActiveMember";

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            // Blazor Web App — InteractiveServer only (Constitution Principle I)
            options.Parameters.InteractiveAuto = false;

            // MVC UI
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            //BLAZOR UI
            options.StyleBundles.Configure(
                BlazorMudBlazorBasicThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/blazor-global-styles.css");
                }
            );
        });
    }

    private void ConfigureVirtualFileSystem(IWebHostEnvironment hostingEnvironment)
    {
        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<ToolShareDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}ToolShare.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<ToolShareDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}ToolShare.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<ToolShareApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}ToolShare.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<ToolShareApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}ToolShare.Application"));
                options.FileSets.ReplaceEmbeddedByPhysical<ToolShareBlazorModule>(hostingEnvironment.ContentRootPath);
            });
        }
    }

    private void ConfigureSwaggerServices(IServiceCollection services)
    {
        services.AddAbpSwaggerGen(
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "ToolShare API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            }
        );
    }


    private void ConfigureMenu(ServiceConfigurationContext context)
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new ToolShareMenuContributor(context.Services.GetConfiguration()));
        });
    }

    private void ConfigureRouter(ServiceConfigurationContext context)
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AppAssembly = typeof(ToolShareBlazorModule).Assembly;
        });
    }

    private void ConfigureAutoApiControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(ToolShareApplicationModule).Assembly);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var env = context.GetEnvironment();
        var app = context.GetApplicationBuilder();

        app.UseAbpRequestLocalization();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }
        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAntiforgery();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ToolShare API");
        });

        app.UseConfiguredEndpoints(builder =>
        {
            builder.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddAdditionalAssemblies(builder.ServiceProvider.GetRequiredService<IOptions<AbpRouterOptions>>().Value.AdditionalAssemblies.ToArray());
        });
    }
}
