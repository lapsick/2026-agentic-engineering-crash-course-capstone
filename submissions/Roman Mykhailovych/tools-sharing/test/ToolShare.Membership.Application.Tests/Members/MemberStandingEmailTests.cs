using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>005-notifications, research R1: the additive <see cref="MemberStandingDto.Email"/> field.</summary>
public class MemberStandingEmailTests : MembershipAuthorizationTestBase
{
    private readonly IMemberStandingAppService _memberStandingAppService;

    public MemberStandingEmailTests()
    {
        _memberStandingAppService = GetRequiredService<IMemberStandingAppService>();
    }

    [Fact]
    public async Task GetAsync_populates_email()
    {
        var email = $"{System.Guid.NewGuid():N}@example.com";
        var member = await EnrolAsAdminAsync("Email Test Member", email: email);

        var standing = await _memberStandingAppService.GetAsync(member.Id);

        standing.IsEnrolled.ShouldBeTrue();
        standing.Email.ShouldBe(email.ToLowerInvariant());
    }

    [Fact]
    public async Task GetByIdsAsync_populates_email()
    {
        var email = $"{System.Guid.NewGuid():N}@example.com";
        var member = await EnrolAsAdminAsync("Email Test Member Batch", email: email);

        var standings = await _memberStandingAppService.GetByIdsAsync(new[] { member.Id });

        standings.ShouldHaveSingleItem();
        standings[0].Email.ShouldBe(email.ToLowerInvariant());
    }

    [Fact]
    public async Task GetByIdentityUserIdAsync_is_unaffected_by_the_email_extension()
    {
        var email = $"{System.Guid.NewGuid():N}@example.com";
        var member = await EnrolAsAdminAsync("Email Test Member Identity Path", email: email);

        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(member.IdentityUserId);

        // Still resolvable, unaffected by this feature; Email is simply not
        // populated on this cached path (contracts/membership-extension.md).
        standing.IsEnrolled.ShouldBeTrue();
        standing.DisplayName.ShouldBe(member.DisplayName);
    }
}
