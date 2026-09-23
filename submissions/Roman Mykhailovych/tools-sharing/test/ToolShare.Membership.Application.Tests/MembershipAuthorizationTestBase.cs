using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ToolShare.Membership.Members;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace ToolShare.Membership;

/// <summary>
/// Shared impersonation and enrolment helpers for the US1 test suites — mirrors
/// <c>ToolShare.Catalog.Authorization.CatalogAuthorizationTestBase</c>.
/// Impersonation is done purely via <see cref="Volo.Abp.Security.Claims.ICurrentPrincipalAccessor.Change"/>
/// with hand-built claims; the enrolment gate additionally requires a real,
/// database-backed <see cref="Member"/> row for whatever identity user id the
/// principal carries (research R10's "membership-gated, not merely
/// authentication-gated" rule applies to Membership's own app services too).
/// </summary>
public abstract class MembershipAuthorizationTestBase : MembershipApplicationTestBase
{
    protected readonly IMemberAppService MemberAppService;
    protected readonly IMemberRepository MemberRepository;

    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IIdentityUserRepository _identityUserRepository;

    protected MembershipAuthorizationTestBase()
    {
        MemberAppService = GetRequiredService<IMemberAppService>();
        MemberRepository = GetRequiredService<IMemberRepository>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
    }

    /// <summary>Same "must be called directly by the caller, never returned across an await" rule as the Catalog equivalent — see that type's remarks.</summary>
    protected IDisposable Impersonate(ClaimsPrincipal principal)
    {
        return _currentPrincipalAccessor.Change(principal);
    }

    protected IDisposable AsAnonymous()
    {
        return Impersonate(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    /// <summary>A genuinely fresh identity with no backing <see cref="Member"/> row anywhere — the "signed in but never enrolled" case (FR-006).</summary>
    protected IDisposable AsAuthenticatedNonMember()
    {
        return Impersonate(BuildPrincipal(Guid.NewGuid(), "never-enrolled-user"));
    }

    /// <summary>An enrolled, Active member holding no Membership permissions — see <see cref="MembershipTestPrincipals.NoGrantsMemberId"/>.</summary>
    protected IDisposable AsMemberWithNoGrants()
    {
        return Impersonate(BuildPrincipal(MembershipTestPrincipals.NoGrantsMemberId, "no-grants-member"));
    }

    /// <summary>Looks up the seeded admin's real identity — does not impersonate; pass the result to <see cref="Impersonate"/> yourself.</summary>
    protected async Task<ClaimsPrincipal> BuildAdminPrincipalAsync()
    {
        var admin = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN")
            ?? throw new InvalidOperationException("The seeded 'admin' user was not found — template seeding did not run.");

        return BuildPrincipal(admin.Id, admin.UserName, "admin");
    }

    /// <summary>Enrols a member as the seeded "admin" and returns the created detail DTO — its <see cref="MemberDetailDto.IdentityUserId"/> is what a caller impersonates to act as this new member.</summary>
    protected async Task<MemberDetailDto> EnrolAsAdminAsync(string displayName, CommunityRole role = CommunityRole.Member, string? email = null)
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            return await MemberAppService.EnrolAsync(new EnrolMemberDto
            {
                DisplayName = displayName,
                Email = email ?? $"{Guid.NewGuid():N}@example.com",
                InitialPassword = "Passw0rd!123",
                Role = role
            });
        }
    }

    protected static ClaimsPrincipal BuildPrincipal(Guid userId, string userName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, userName)
        };
        claims.AddRange(roles.Select(role => new Claim(AbpClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }
}
