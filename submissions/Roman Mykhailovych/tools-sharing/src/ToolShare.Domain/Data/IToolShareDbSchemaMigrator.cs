using System.Threading.Tasks;

namespace ToolShare.Data;

public interface IToolShareDbSchemaMigrator
{
    Task MigrateAsync();
}
