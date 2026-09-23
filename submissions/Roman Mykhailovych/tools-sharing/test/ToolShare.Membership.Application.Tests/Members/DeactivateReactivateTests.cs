using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>FR-005, SC-005: reactivation preserves the previous rating; the record and history stay retrievable while deactivated.</summary>
public class DeactivateReactivateTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Deactivating_then_reactivating_preserves_the_rating_and_keeps_the_record_visible()
    {
        var member = await EnrolAsAdminAsync("Deactivate Target");

        var adminPrincipal = await BuildAdminPrincipalAsync();
        MemberDetailDto deactivated;
        using (Impersonate(adminPrincipal))
        {
            deactivated = await MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "No longer participating",
                ConcurrencyStamp = member.ConcurrencyStamp
            });
        }

        deactivated.Status.ShouldBe(MembershipStatus.Deactivated);
        deactivated.CurrentRating.ShouldBe(100);

        // SC-005: the record and history stay retrievable while deactivated.
        var stillVisible = await MemberRepository.GetWithHistoryAsync(member.Id);
        stillVisible.ShouldNotBeNull();
        stillVisible!.Status.ShouldBe(MembershipStatus.Deactivated);
        stillVisible.StandingHistory.Count.ShouldBe(2);

        using (Impersonate(adminPrincipal))
        {
            var reactivated = await MemberAppService.ReactivateAsync(member.Id, new ReactivateMemberDto
            {
                ConcurrencyStamp = deactivated.ConcurrencyStamp
            });

            reactivated.Status.ShouldBe(MembershipStatus.Active);
            // FR-005: reactivation must not touch the rating.
            reactivated.CurrentRating.ShouldBe(100);
        }
    }

    [Fact]
    public async Task Deactivating_an_already_deactivated_member_is_rejected()
    {
        var member = await EnrolAsAdminAsync("Double Deactivate Target");
        var adminPrincipal = await BuildAdminPrincipalAsync();

        using (Impersonate(adminPrincipal))
        {
            var deactivated = await MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "First deactivation",
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            var exception = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "Second deactivation",
                ConcurrencyStamp = deactivated.ConcurrencyStamp
            }));

            exception.Code.ShouldBe(MembershipDomainErrorCodes.AlreadyDeactivated);
        }
    }
}
