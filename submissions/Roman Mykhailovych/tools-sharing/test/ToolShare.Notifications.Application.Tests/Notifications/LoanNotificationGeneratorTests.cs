using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>FR-001, FR-002, FR-006: raising a <see cref="LendingNotificationDueEto"/> produces exactly one in-app notification.</summary>
public class LoanNotificationGeneratorTests : NotificationsAuthorizationTestBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IMemberRepository _memberRepository;

    public LoanNotificationGeneratorTests()
    {
        _notificationRepository = GetRequiredService<INotificationRepository>();
        _memberRepository = GetRequiredService<IMemberRepository>();
    }

    [Fact]
    public async Task Return_reminder_event_creates_a_notification_naming_the_tool_and_date()
    {
        var (_, tool, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);
        member.ShouldNotBeNull();

        var loanId = Guid.NewGuid();
        var plannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = loanId,
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = plannedReturnDate,
            RaisedAt = DateTime.UtcNow
        });

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);

        notifications.ShouldHaveSingleItem();
        notifications[0].Kind.ShouldBe(NotificationKind.LoanReturnReminder);
        notifications[0].OriginatingLoanId.ShouldBe(loanId);
        notifications[0].DisplayText.ShouldContain(tool.Name);
        notifications[0].DisplayText.ShouldContain(plannedReturnDate.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Overdue_event_creates_a_distinct_notification()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var member = await _memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        var loanId = Guid.NewGuid();

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = loanId,
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.Overdue,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RaisedAt = DateTime.UtcNow
        });

        var notifications = await _notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10);

        notifications.ShouldHaveSingleItem();
        notifications[0].Kind.ShouldBe(NotificationKind.LoanOverdue);
    }
}
