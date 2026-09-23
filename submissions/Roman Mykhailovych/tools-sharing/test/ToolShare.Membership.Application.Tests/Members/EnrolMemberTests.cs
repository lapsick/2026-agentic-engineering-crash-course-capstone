using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>FR-001, FR-002, FR-003, research R2: one action creates both the identity account and the member record, in one transaction.</summary>
public class EnrolMemberTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Enrolling_creates_both_the_identity_account_and_the_member_record()
    {
        var email = $"{System.Guid.NewGuid():N}@example.com";
        var member = await EnrolAsAdminAsync("Alice Example", email: email);

        member.Status.ShouldBe(MembershipStatus.Active);
        member.Role.ShouldBe(CommunityRole.Member);
        member.CurrentRating.ShouldBe(100);

        var identityUser = await GetRequiredService<IIdentityUserRepository>().GetAsync(member.IdentityUserId);
        identityUser.Email.ShouldBe(email);

        var withHistory = await MemberRepository.GetWithHistoryAsync(member.Id);
        withHistory.ShouldNotBeNull();
        withHistory!.StandingHistory.Count.ShouldBe(1);
        withHistory.StandingHistory.Single().Kind.ShouldBe(MemberStandingChangeKind.Enrolled);
    }

    [Fact]
    public async Task Enrolling_with_a_non_default_role_appends_both_the_enrolled_and_role_changed_entries()
    {
        var member = await EnrolAsAdminAsync("Bob Librarian", CommunityRole.Librarian);

        member.Role.ShouldBe(CommunityRole.Librarian);

        var withHistory = await MemberRepository.GetWithHistoryAsync(member.Id);
        withHistory!.StandingHistory.Count.ShouldBe(2);
        withHistory.StandingHistory.ElementAt(0).Kind.ShouldBe(MemberStandingChangeKind.Enrolled);
        withHistory.StandingHistory.ElementAt(1).Kind.ShouldBe(MemberStandingChangeKind.RoleChanged);
    }

    /// <summary>
    /// Forces a failure AFTER the identity account is provisioned (its own
    /// <c>SaveChangesAsync</c> already ran) but BEFORE the Member aggregate is
    /// persisted: an out-of-range <see cref="CommunityRole"/> value passes
    /// <see cref="EnrolMemberDto"/>'s validation (it carries no range/enum
    /// attribute on <c>Role</c>) but has no matching ABP role for
    /// <c>IMemberIdentityProvisioner.SetRoleAsync</c> to find, so it throws.
    /// Asserts research R2: both writes share one ambient database transaction —
    /// the identity user created moments earlier is rolled back too, because the
    /// whole application-service call is one ABP unit of work.
    /// </summary>
    [Fact]
    public async Task A_forced_failure_partway_through_rolls_back_both_writes()
    {
        var email = $"{System.Guid.NewGuid():N}@example.com";
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            await Should.ThrowAsync<System.InvalidOperationException>(() => MemberAppService.EnrolAsync(new EnrolMemberDto
            {
                DisplayName = "Rollback Target",
                Email = email,
                InitialPassword = "Passw0rd!123",
                Role = (CommunityRole)99
            }));
        }

        var orphanedIdentityUser = await GetRequiredService<IIdentityUserRepository>().FindByNormalizedEmailAsync(email.ToUpperInvariant());
        orphanedIdentityUser.ShouldBeNull();

        var orphanedMember = await MemberRepository.FindAsync(m => m.Email == email.ToLowerInvariant());
        orphanedMember.ShouldBeNull();
    }
}
