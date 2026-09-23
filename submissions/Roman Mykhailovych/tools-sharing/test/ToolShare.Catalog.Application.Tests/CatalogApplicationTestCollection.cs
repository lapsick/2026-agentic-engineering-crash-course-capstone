using Xunit;

namespace ToolShare.Catalog;

public static class CatalogApplicationTestConsts
{
    public const string CollectionDefinitionName = "CatalogApplicationTestCollection";
}

[CollectionDefinition(CatalogApplicationTestConsts.CollectionDefinitionName)]
public class CatalogApplicationTestCollection : ICollectionFixture<CatalogApplicationTestFixture>
{
}
