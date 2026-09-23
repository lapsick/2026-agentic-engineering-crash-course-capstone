using ToolShare.Samples;
using Xunit;

namespace ToolShare.EntityFrameworkCore.Applications;

[Collection(ToolShareTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<ToolShareEntityFrameworkCoreTestModule>
{

}
