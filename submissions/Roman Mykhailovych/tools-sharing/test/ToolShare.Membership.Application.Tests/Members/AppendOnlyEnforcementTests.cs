using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ToolShare.Membership.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-020: proves append-only holds beyond the domain-level guarantee already
/// asserted by <c>StandingHistoryTests</c> (Phase 2, T020) — at the repository
/// and application surface (no update/delete path is exposed anywhere in this
/// module's own code) and at the EF Core persistence boundary itself (a direct
/// attempt to mark a <see cref="MemberStandingChange"/> Modified or Deleted and
/// call <c>SaveChanges</c> is rejected by <see cref="MembershipDbContext"/>
/// itself, not merely discouraged by convention).
/// </summary>
public class AppendOnlyEnforcementTests : MembershipAuthorizationTestBase
{
    [Fact]
    public void The_entity_exposes_no_public_update_or_delete_method()
    {
        var historyType = typeof(MemberStandingChange);

        historyType.GetMethods()
            .Where(m => m.DeclaringType == historyType)
            .Any(m => m.Name is "Update" or "Delete" or "Remove")
            .ShouldBeFalse();

        foreach (var property in historyType.GetProperties())
        {
            property.SetMethod?.IsPublic.ShouldNotBe(true, $"{property.Name} must not have a public setter");
        }
    }

    /// <summary>
    /// ABP's <c>AddDefaultRepositories(includeAllEntities: true)</c> does
    /// register a generic <c>IRepository&lt;MemberStandingChange, Guid&gt;</c>
    /// in DI (it exposes <c>UpdateAsync</c>/<c>DeleteAsync</c> like any other
    /// default repository) — nothing prevents that registration from existing.
    /// What HR-02 actually requires is that no type in this module's own
    /// domain or application layer ever asks for it. Proven here by reflecting
    /// over every constructor in both assemblies for a parameter whose generic
    /// repository argument is <see cref="MemberStandingChange"/>.
    /// </summary>
    [Fact]
    public void No_domain_or_application_type_depends_on_a_writable_repository_over_a_standing_change()
    {
        var domainAssembly = typeof(Member).Assembly;
        var applicationAssembly = typeof(MemberAppService).Assembly;

        var offendingParameters = domainAssembly.GetTypes()
            .Concat(applicationAssembly.GetTypes())
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Where(p => IsRepositoryOverStandingChange(p.ParameterType))
            .ToList();

        offendingParameters.ShouldBeEmpty();
    }

    private static bool IsRepositoryOverStandingChange(Type type)
    {
        if (!type.IsGenericType)
        {
            return false;
        }

        return type.GetGenericArguments().Any(arg => arg == typeof(MemberStandingChange));
    }

    [Fact]
    public async Task A_direct_EF_Core_attempt_to_modify_a_standing_change_is_rejected()
    {
        var member = await EnrolAsAdminAsync("Append Only Modify Enforcement Target");
        var seeded = await MemberRepository.GetWithHistoryAsync(member.Id);
        var originalEntryId = seeded!.StandingHistory.Single().Id;

        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            using var uow = uowManager.Begin(new AbpUnitOfWorkOptions());

            var dbContextProvider = scope.ServiceProvider.GetRequiredService<IDbContextProvider<MembershipDbContext>>();
            var dbContext = await dbContextProvider.GetDbContextAsync();

            var tracked = await dbContext.Set<MemberStandingChange>().FirstAsync(h => h.Id == originalEntryId);

            // A "direct EF Core update attempt": bypasses the entity's private
            // setters entirely via EF's own change-tracking API, not a
            // hand-rolled repository call.
            dbContext.Entry(tracked).Property(nameof(MemberStandingChange.Reason)).CurrentValue = "tampered";

            var exception = await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
            exception.Message.ShouldContain("append-only");

            // Deliberately never completed — the attempted mutation must never
            // reach the database, and the guard fires before any SQL is sent.
        }

        var reloaded = await MemberRepository.GetWithHistoryAsync(member.Id);
        reloaded!.StandingHistory.Single().Reason.ShouldBeNull();
    }

    [Fact]
    public async Task A_direct_EF_Core_attempt_to_delete_a_standing_change_is_rejected()
    {
        var member = await EnrolAsAdminAsync("Append Only Delete Enforcement Target");
        var seeded = await MemberRepository.GetWithHistoryAsync(member.Id);
        var originalEntryId = seeded!.StandingHistory.Single().Id;

        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            using var uow = uowManager.Begin(new AbpUnitOfWorkOptions());

            var dbContextProvider = scope.ServiceProvider.GetRequiredService<IDbContextProvider<MembershipDbContext>>();
            var dbContext = await dbContextProvider.GetDbContextAsync();

            var tracked = await dbContext.Set<MemberStandingChange>().FirstAsync(h => h.Id == originalEntryId);

            dbContext.Set<MemberStandingChange>().Remove(tracked);

            var exception = await Should.ThrowAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
            exception.Message.ShouldContain("append-only");
        }

        var reloaded = await MemberRepository.GetWithHistoryAsync(member.Id);
        reloaded!.StandingHistory.Count.ShouldBe(1);
    }
}
