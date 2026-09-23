using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Users;

namespace ToolShare.Catalog.ToolInstances;

public class ToolInstanceManager : DomainService
{
    private readonly IToolInstanceRepository _toolInstanceRepository;
    private readonly ICurrentUser _currentUser;

    public ToolInstanceManager(IToolInstanceRepository toolInstanceRepository, ICurrentUser currentUser)
    {
        _toolInstanceRepository = toolInstanceRepository;
        _currentUser = currentUser;
    }

    /// <summary>Enforces IR-01–IR-03.</summary>
    public async Task<ToolInstance> CreateAsync(Guid toolId, string serialNumber, ToolCondition condition, string? notes = null)
    {
        var instance = new ToolInstance(
            GuidGenerator.Create(),
            toolId,
            serialNumber,
            condition,
            Clock.Now,
            _currentUser.Id,
            notes);

        await ValidateSerialNumberUniquenessAsync(instance.NormalizedSerialNumber, excludedId: null);

        return instance;
    }

    /// <summary>Re-checks IR-01/IR-02 excluding the instance itself.</summary>
    public async Task ChangeSerialNumberAsync(ToolInstance instance, string newSerialNumber)
    {
        var previousNormalized = instance.NormalizedSerialNumber;
        instance.SetSerialNumber(newSerialNumber);

        if (instance.NormalizedSerialNumber == previousNormalized)
        {
            return;
        }

        await ValidateSerialNumberUniquenessAsync(instance.NormalizedSerialNumber, instance.Id);
    }

    private async Task ValidateSerialNumberUniquenessAsync(string normalizedSerialNumber, Guid? excludedId)
    {
        if (await _toolInstanceRepository.AnyBySerialNumberAsync(normalizedSerialNumber, excludedId))
        {
            throw new BusinessException("Catalog:SerialNumberAlreadyExists").WithData("serialNumber", normalizedSerialNumber);
        }
    }
}
