using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ToolShare.Data;

/* This is used if database provider does't define
 * IToolShareDbSchemaMigrator implementation.
 */
public class NullToolShareDbSchemaMigrator : IToolShareDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
