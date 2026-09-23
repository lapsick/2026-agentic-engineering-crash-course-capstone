using Xunit;

namespace ToolShare.Notifications;

public static class NotificationsApplicationTestConsts
{
    public const string CollectionDefinitionName = "NotificationsApplicationTestCollection";
}

[CollectionDefinition(NotificationsApplicationTestConsts.CollectionDefinitionName)]
public class NotificationsApplicationTestCollection : ICollectionFixture<NotificationsApplicationTestFixture>
{
}
