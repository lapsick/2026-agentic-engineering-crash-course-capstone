using Xunit;

namespace ToolShare.Lending;

public static class LendingApplicationTestConsts
{
    public const string CollectionDefinitionName = "LendingApplicationTestCollection";
}

[CollectionDefinition(LendingApplicationTestConsts.CollectionDefinitionName)]
public class LendingApplicationTestCollection : ICollectionFixture<LendingApplicationTestFixture>
{
}
