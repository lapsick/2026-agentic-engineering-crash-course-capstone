using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Xunit;

namespace ToolShare.Membership.Seeding;

/// <summary>
/// Exercises <see cref="MembershipDataSeedContributor"/> (Phase 2) through the
/// application-test harness: a second seed run must leave exactly one
/// <see cref="CommunityRules.CommunityRules"/> row (CRR-02) and exactly one
/// bootstrap administrator <see cref="Member"/> row (FR-010) — mirrors
/// <c>CatalogDataSeedIdempotenceTests</c>.
/// </summary>
public class MembershipSeedIdempotenceTests : MembershipApplicationTestBase
{
    private readonly MembershipDataSeedContributor _seedContributor;
    private readonly ICommunityRulesRepository _communityRulesRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IIdentityUserRepository _identityUserRepository;

    public MembershipSeedIdempotenceTests()
    {
        _seedContributor = GetRequiredService<MembershipDataSeedContributor>();
        _communityRulesRepository = GetRequiredService<ICommunityRulesRepository>();
        _memberRepository = GetRequiredService<IMemberRepository>();
        _identityUserRepository = GetRequiredService<IIdentityUserRepository>();
    }

    [Fact]
    public async Task Running_the_seeder_twice_leaves_exactly_one_rules_row_and_one_bootstrap_member_row()
    {
        var adminUser = await _identityUserRepository.FindByNormalizedUserNameAsync("ADMIN")
            ?? throw new InvalidOperationException("The seeded 'admin' user was not found — template seeding did not run.");

        var context = new DataSeedContext()
            .WithProperty(MembershipDataSeedContributor.AdminIdentityUserIdPropertyName, adminUser.Id)
            .WithProperty(MembershipDataSeedContributor.AdminDisplayNamePropertyName, adminUser.Name)
            .WithProperty(MembershipDataSeedContributor.AdminEmailPropertyName, adminUser.Email);

        await _seedContributor.SeedAsync(context);
        await _seedContributor.SeedAsync(context);

        var allRules = await _communityRulesRepository.GetListAsync();
        allRules.Count.ShouldBe(1);
        allRules.Single().Id.ShouldBe(CommunityRulesConsts.SingletonId);

        var allMembers = await _memberRepository.GetListAsync();
        allMembers.Count(m => m.IdentityUserId == adminUser.Id).ShouldBe(1);
    }
}
