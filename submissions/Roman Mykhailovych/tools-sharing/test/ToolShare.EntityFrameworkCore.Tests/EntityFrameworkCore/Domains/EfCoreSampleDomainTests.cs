using ToolShare.Samples;
using Xunit;

namespace ToolShare.EntityFrameworkCore.Domains;

[Collection(ToolShareTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<ToolShareEntityFrameworkCoreTestModule>
{

}
