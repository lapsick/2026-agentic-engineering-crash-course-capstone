using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace ToolShare.Catalog;

[DependsOn(
    typeof(CatalogDomainModule),
    typeof(CatalogApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpBlobStoringFileSystemModule),
    typeof(AbpMapperlyModule)
    )]
public class CatalogApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<CatalogPhotoOptions>(configuration.GetSection("Catalog:Photos"));

        context.Services.AddMapperlyObjectMapper<CatalogApplicationModule>();

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure<ToolInstancePhotoContainer>(container =>
            {
                container.UseFileSystem(fileSystem =>
                {
                    var basePath = configuration["Catalog:Photos:BasePath"];
                    fileSystem.BasePath = string.IsNullOrWhiteSpace(basePath)
                        ? Path.Combine(AppContext.BaseDirectory, "blobs", "tool-instance-photos")
                        : basePath;
                });
            });
        });
    }
}
