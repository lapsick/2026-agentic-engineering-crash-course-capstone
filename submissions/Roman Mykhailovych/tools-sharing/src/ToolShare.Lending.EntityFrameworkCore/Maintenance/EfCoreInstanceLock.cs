using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ToolShare.Lending.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace ToolShare.Lending.Maintenance;

/// <summary>
/// <see cref="IInstanceLock"/> as a PostgreSQL transaction-scoped advisory
/// lock, taken on Lending's own connection inside the ambient unit-of-work
/// transaction — it touches no Catalog object. Two instance ids hashing to
/// the same key merely serialize a little more than necessary.
/// </summary>
public class EfCoreInstanceLock : IInstanceLock, ITransientDependency
{
    private readonly IDbContextProvider<LendingDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public EfCoreInstanceLock(IDbContextProvider<LendingDbContext> dbContextProvider, IUnitOfWorkManager unitOfWorkManager)
    {
        _dbContextProvider = dbContextProvider;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task LockInstanceAsync(Guid toolInstanceId, CancellationToken cancellationToken = default)
    {
        if (_unitOfWorkManager.Current?.Options.IsTransactional != true)
        {
            // Outside a transaction pg_advisory_xact_lock would be released at
            // the end of this one statement — i.e. not lock anything useful.
            throw new AbpException($"{nameof(IInstanceLock)} requires a transactional unit of work.");
        }

        var dbContext = await _dbContextProvider.GetDbContextAsync();

        await dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended('lending:instance:' || {0}::text, 0))",
            new object[] { toolInstanceId },
            cancellationToken);
    }
}
