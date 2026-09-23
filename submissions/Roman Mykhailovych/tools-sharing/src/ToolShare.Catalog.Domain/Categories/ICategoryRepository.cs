using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ToolShare.Catalog.Categories;

public interface ICategoryRepository : IRepository<Category, Guid>
{
    Task<Category?> FindByNormalizedNameAsync(string normalizedName, Guid? excludedId = null, CancellationToken cancellationToken = default);

    Task<bool> AnyToolAssignedAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<int> CountToolsAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
