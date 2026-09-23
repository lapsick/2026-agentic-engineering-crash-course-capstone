namespace ToolShare.Notifications;

/// <summary>Field-length limits shared between validation and EF Core mapping.</summary>
public static class NotificationsDomainSharedConsts
{
    public const int DisplayTextMaxLength = 512;

    public const int FailureDetailMaxLength = 512;
}
