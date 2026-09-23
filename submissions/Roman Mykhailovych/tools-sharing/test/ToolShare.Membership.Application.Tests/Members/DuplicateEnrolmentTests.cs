using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.Members;
using Volo.Abp;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>FR-002, SC-002: duplicate email and duplicate identity id are both rejected, naming the conflicting value.</summary>
public class DuplicateEnrolmentTests : MembershipAuthorizationTestBase
{
    [Fact]
    public async Task Enrolling_with_an_email_already_in_use_is_rejected()
    {
        var email = $"{System.Guid.NewGuid():N}@example.com";
        await EnrolAsAdminAsync("First Person", email: email);

        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var exception = await Should.ThrowAsync<System.Exception>(() => MemberAppService.EnrolAsync(new EnrolMemberDto
            {
                DisplayName = "Second Person",
                Email = email,
                InitialPassword = "Passw0rd!123"
            }));

            // Surfaced unwrapped from the identity store (research R2) — the
            // message must name the actual conflict, not a generic failure.
            exception.Message.ToLowerInvariant().ShouldContain(email.ToLowerInvariant());
        }
    }

    /// <summary>
    /// MR-02: since enrolment always mints a brand-new identity user id, the only
    /// way to exercise the identity-uniqueness guard directly is at the
    /// <see cref="MemberManager"/> level — this proves the guard the app service
    /// relies on, without needing to fabricate a colliding provisioner.
    /// </summary>
    [Fact]
    public async Task Duplicate_identity_user_id_is_rejected_at_the_domain_level()
    {
        var member = await EnrolAsAdminAsync("Third Person");

        var memberManager = GetRequiredService<MemberManager>();
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            memberManager.CreateAsync(member.IdentityUserId, "Duplicate Identity", "duplicate@example.com", System.DateTime.UtcNow, enrolledByUserId: null));

        exception.Code.ShouldBe(MembershipDomainErrorCodes.IdentityAlreadyEnrolled);
    }
}
