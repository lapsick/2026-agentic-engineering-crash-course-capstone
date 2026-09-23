using Xunit;

namespace ToolShare.Membership;

public static class MembershipApplicationTestConsts
{
    public const string CollectionDefinitionName = "MembershipApplicationTestCollection";
}

[CollectionDefinition(MembershipApplicationTestConsts.CollectionDefinitionName)]
public class MembershipApplicationTestCollection : ICollectionFixture<MembershipApplicationTestFixture>
{
}
