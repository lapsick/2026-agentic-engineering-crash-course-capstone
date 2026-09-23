using Volo.Abp.Application.Dtos;

namespace ToolShare.Notifications.Notifications;

public class GetMyNotificationsInput : PagedResultRequestDto
{
    public override int MaxResultCount
    {
        get => base.MaxResultCount;
        set => base.MaxResultCount = value > MaxMaxResultCount ? MaxMaxResultCount : value;
    }
}
