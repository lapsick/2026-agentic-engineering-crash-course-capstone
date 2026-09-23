using Xunit;

namespace ToolShare.EntityFrameworkCore;

[CollectionDefinition(ToolShareTestConsts.CollectionDefinitionName)]
public class ToolShareEntityFrameworkCoreCollection : ICollectionFixture<ToolShareEntityFrameworkCoreFixture>
{

}
