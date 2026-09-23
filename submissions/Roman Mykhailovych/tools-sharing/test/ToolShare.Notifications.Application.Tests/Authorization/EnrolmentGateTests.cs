using System.Threading.Tasks;
using Shouldly;
using ToolShare.Notifications.Notifications;
using Volo.Abp.Authorization;
using Xunit;

namespace ToolShare.Notifications.Authorization;

/// <summary>An authenticated identity with no backing Membership enrolment is refused for every Notifications operation, mirroring 003's own gate tests.</summary>
public class EnrolmentGateTests : NotificationsAuthorizationTestBase
{
    private readonly IMyNotificationsAppService _myNotificationsAppService;
    private readonly INotificationAuditAppService _notificationAuditAppService;

    public EnrolmentGateTests()
    {
        _myNotificationsAppService = GetRequiredService<IMyNotificationsAppService>();
        _notificationAuditAppService = GetRequiredService<INotificationAuditAppService>();
    }

    [Fact]
    public async Task An_authenticated_non_member_cannot_list_their_own_notifications()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _myNotificationsAppService.GetListAsync(new GetMyNotificationsInput()));
        }
    }

    [Fact]
    public async Task An_authenticated_non_member_cannot_get_the_unread_count()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _myNotificationsAppService.GetUnreadCountAsync());
        }
    }

    [Fact]
    public async Task An_authenticated_non_member_cannot_resolve_their_member_id()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _myNotificationsAppService.GetMyMemberIdAsync());
        }
    }

    [Fact]
    public async Task An_authenticated_non_member_cannot_use_the_audit_surface()
    {
        using (AsAuthenticatedNonMember())
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => _notificationAuditAppService.GetAsync(System.Guid.NewGuid()));
        }
    }
}
