using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>FR-001a, research R4: an enrolled account has <see cref="IdentityUser.ShouldChangePasswordOnNextLogin"/> set, via ABP's native mechanism.</summary>
public class FirstSignInPasswordChangeTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task An_enrolled_account_must_change_its_password_on_next_sign_in()
    {
        var member = await EnrolAsAdminAsync("Forced Password Change Target");

        var identityUser = await GetRequiredService<IIdentityUserRepository>().GetAsync(member.IdentityUserId);

        identityUser.ShouldChangePasswordOnNextLogin.ShouldBeTrue();
    }
}
