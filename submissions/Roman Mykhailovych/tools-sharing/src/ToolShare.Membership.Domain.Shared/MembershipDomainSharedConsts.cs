namespace ToolShare.Membership;

public static class MembershipDomainSharedConsts
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 128;

    public const int EmailMaxLength = 256;

    public const int StatusChangeReasonMaxLength = 512;
    public const int AdjustmentReasonMaxLength = 512;

    public const int MinRating = 0;
    public const int MaxRating = 100;
    public const int DefaultRating = 100;
}
