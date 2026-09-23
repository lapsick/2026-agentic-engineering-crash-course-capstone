using System;
using System.Threading.Tasks;
using Shouldly;
using ToolShare.Lending.Loans;
using ToolShare.Membership.Members;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Notifications.Notifications;

/// <summary>SC-006: an Administrator can retrieve any notification's full delivery history; a Member/Librarian is denied.</summary>
public class NotificationAuditTests : NotificationsAuthorizationTestBase
{
    [Fact]
    public async Task Administrator_can_read_any_notifications_full_delivery_history()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var member = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var notification = (await notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10))[0];

        // The default test-runner principal carries only the built-in
        // Identity "admin" role claim, not Membership's own "Administrator"
        // role (granted Notifications.Audit) — the real seeded admin
        // identity is needed here, exactly as Lending's own permission-gated
        // tests use BuildAdminPrincipalAsync rather than the default principal.
        var adminPrincipal = await BuildAdminPrincipalAsync();
        using (Impersonate(adminPrincipal))
        {
            var auditAppService = GetRequiredService<INotificationAuditAppService>();
            var audit = await auditAppService.GetAsync(notification.Id);

            audit.MemberId.ShouldBe(member.Id);
            audit.DeliveryRecords.Count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task Member_is_denied_access_to_the_audit_surface()
    {
        var (_, _, instance) = await SeedCatalogDataAsync();
        var memberRepository = GetRequiredService<IMemberRepository>();
        var member = await memberRepository.FindByIdentityUserIdAsync(NotificationsTestPrincipals.NoGrantsUserId);

        await LocalEventBus.PublishAsync(new LendingNotificationDueEto
        {
            LoanId = Guid.NewGuid(),
            MemberId = member!.Id,
            ToolInstanceId = instance.Id,
            Kind = LendingNotificationKind.ReturnReminder,
            PlannedReturnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            RaisedAt = DateTime.UtcNow
        });

        var notificationRepository = GetRequiredService<INotificationRepository>();
        var notification = (await notificationRepository.GetPagedListForMemberAsync(member.Id, 0, 10))[0];

        using (Impersonate(BuildPrincipal(NotificationsTestPrincipals.NoGrantsUserId, "no-grants-user")))
        {
            var auditAppService = GetRequiredService<INotificationAuditAppService>();
            await Should.ThrowAsync<AbpAuthorizationException>(() => auditAppService.GetAsync(notification.Id));
        }
    }
}
