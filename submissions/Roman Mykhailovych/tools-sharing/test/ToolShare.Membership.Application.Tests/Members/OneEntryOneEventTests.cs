using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// FR-017a/SC-010: enrol -&gt; deactivate -&gt; reactivate -&gt; role change -&gt; two
/// rating outcomes must yield exactly six standing-history entries and exactly
/// six <see cref="MemberStandingChangedEto"/> events, in a 1:1 correspondence —
/// same kind, same resulting values, same order. Registers a probe
/// <see cref="ILocalEventHandler{TEvent}"/>, mirroring Catalog's
/// <c>ToolInstanceStateChangedEventTests</c>.
/// </summary>
public class OneEntryOneEventTests : MembershipAuthorizationTestBase
{
    private readonly MemberStandingChangedEventCapture _capture;

    public OneEntryOneEventTests()
    {
        _capture = GetRequiredService<MemberStandingChangedEventCapture>();
    }

    [Fact]
    public async Task Enrol_deactivate_reactivate_role_change_and_two_outcomes_yield_six_entries_and_six_matching_events()
    {
        var adminPrincipal = await BuildAdminPrincipalAsync();

        MemberDetailDto member;
        using (Impersonate(adminPrincipal))
        {
            member = await MemberAppService.EnrolAsync(new EnrolMemberDto
            {
                DisplayName = "One Entry One Event Target",
                Email = $"{Guid.NewGuid():N}@example.com",
                InitialPassword = "Passw0rd!123"
            });

            var afterDeactivate = await MemberAppService.DeactivateAsync(member.Id, new DeactivateMemberDto
            {
                Reason = "Sequence step",
                ConcurrencyStamp = member.ConcurrencyStamp
            });

            var afterReactivate = await MemberAppService.ReactivateAsync(member.Id, new ReactivateMemberDto
            {
                ConcurrencyStamp = afterDeactivate.ConcurrencyStamp
            });

            var afterRoleChange = await MemberAppService.ChangeRoleAsync(member.Id, new ChangeMemberRoleDto
            {
                Role = CommunityRole.Librarian,
                ConcurrencyStamp = afterReactivate.ConcurrencyStamp
            });

            var afterFirstOutcome = await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = -10,
                Reason = "First outcome",
                ConcurrencyStamp = afterRoleChange.ConcurrencyStamp
            });

            await MemberAppService.AdjustRatingAsync(member.Id, new AdjustMemberRatingDto
            {
                Points = 5,
                Reason = "Second outcome",
                ConcurrencyStamp = afterFirstOutcome.ConcurrencyStamp
            });

            var history = await MemberAppService.GetStandingHistoryAsync(member.Id);

            history.Count.ShouldBe(6);

            var events = _capture.Events.Where(e => e.MemberId == member.Id).ToList();
            events.Count.ShouldBe(6);

            for (var i = 0; i < 6; i++)
            {
                var entry = history[i];
                var evt = events[i];

                evt.Kind.ShouldBe(entry.Kind);
                entry.PreviousStatus.ShouldBe(evt.PreviousStatus);
                entry.NewStatus.ShouldBe((MembershipStatus?)evt.NewStatus);
                entry.PreviousRole.ShouldBe(evt.PreviousRole);
                entry.NewRole.ShouldBe((CommunityRole?)evt.NewRole);
                evt.OutcomeType.ShouldBe(entry.OutcomeType);
                evt.OccurrenceId.ShouldBe(entry.OccurrenceId);
                evt.Reason.ShouldBe(entry.Reason);
                // Compared with a tolerance: the in-memory event carries full
                // tick precision, while entry.ChangedAt round-tripped through
                // PostgreSQL's microsecond timestamp precision — same reasoning
                // as MyMembershipTests' EnrolledAt comparison.
                evt.ChangedAt.ShouldBe(entry.ChangedAt, TimeSpan.FromSeconds(1));
                evt.ChangedByUserId.ShouldBe(entry.ChangedByUserId);

                if (entry.Kind == MemberStandingChangeKind.RatingOutcome)
                {
                    evt.NewRating.ShouldBe(entry.ResultingRating!.Value);
                }
            }

            history[0].Kind.ShouldBe(MemberStandingChangeKind.Enrolled);
            history[1].Kind.ShouldBe(MemberStandingChangeKind.StatusChanged);
            history[2].Kind.ShouldBe(MemberStandingChangeKind.StatusChanged);
            history[3].Kind.ShouldBe(MemberStandingChangeKind.RoleChanged);
            history[4].Kind.ShouldBe(MemberStandingChangeKind.RatingOutcome);
            history[5].Kind.ShouldBe(MemberStandingChangeKind.RatingOutcome);
        }
    }
}

/// <summary>Per-test-instance capture buffer (a fresh DI container is built per test method).</summary>
public class MemberStandingChangedEventCapture : ISingletonDependency
{
    public List<MemberStandingChangedEto> Events { get; } = new();
}

/// <summary>
/// The probe handler itself (FR-017a/SC-010): discovered purely by ABP's
/// conventional registration of this test assembly — no explicit wiring
/// anywhere, mirroring Catalog's <c>ToolInstanceStateChangedProbeHandler</c>.
/// </summary>
public class MemberStandingChangedProbeHandler : ILocalEventHandler<MemberStandingChangedEto>, ITransientDependency
{
    private readonly MemberStandingChangedEventCapture _capture;

    public MemberStandingChangedProbeHandler(MemberStandingChangedEventCapture capture)
    {
        _capture = capture;
    }

    public Task HandleEventAsync(MemberStandingChangedEto eventData)
    {
        _capture.Events.Add(eventData);
        return Task.CompletedTask;
    }
}
