using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// A specific physical unit of a <see cref="Tools.Tool"/>, tracked as its own
/// aggregate root because it has its own lifecycle and history and is the thing
/// downstream modules (Lending) will reference by id.
/// </summary>
public class ToolInstance : FullAuditedAggregateRoot<Guid>
{
    private static readonly Regex SerialNumberPattern = new("^[A-Za-z0-9][A-Za-z0-9\\-_/.]*$", RegexOptions.Compiled);

    public virtual Guid ToolId { get; private set; }

    public virtual string SerialNumber { get; private set; } = null!;

    /// <summary>
    /// Derived from <see cref="SerialNumber"/>. Carries the catalog-wide unique
    /// index (IR-02, FR-003, SC-004) — <c>rh-001</c> and <c>RH-001</c> collide.
    /// </summary>
    public virtual string NormalizedSerialNumber { get; private set; } = null!;

    public virtual ToolCondition Condition { get; private set; }

    public virtual ToolInstanceCirculationState CirculationState { get; private set; }

    public virtual string? RetirementReason { get; private set; }

    public virtual DateTime? RetiredAt { get; private set; }

    public virtual string? Notes { get; private set; }

    public virtual ICollection<ToolInstancePhoto> Photos { get; protected set; }

    public virtual ICollection<ToolInstanceStateChange> StateHistory { get; protected set; }

    /// <summary>
    /// Derived, not persisted. Catalog's own notion of availability: physically
    /// in the pool and serviceable. Says nothing about loans/maintenance, which
    /// Catalog does not know about (see contracts/catalog-public-contracts.md).
    /// </summary>
    public virtual bool IsAvailable =>
        CirculationState == ToolInstanceCirculationState.InCirculation && Condition != ToolCondition.Damaged;

    protected ToolInstance()
    {
        Photos = new List<ToolInstancePhoto>();
        StateHistory = new List<ToolInstanceStateChange>();
    }

    /// <summary>
    /// Registers a new instance. Enforces IR-01–IR-03: any of the four
    /// <see cref="ToolCondition"/> values is accepted, circulation starts
    /// <see cref="ToolInstanceCirculationState.InCirculation"/>, and the first
    /// history row is appended with both Previous* values null.
    /// </summary>
    public ToolInstance(
        Guid id,
        Guid toolId,
        string serialNumber,
        ToolCondition condition,
        DateTime registeredAt,
        Guid? registeredByUserId,
        string? notes = null)
        : base(id)
    {
        Photos = new List<ToolInstancePhoto>();
        StateHistory = new List<ToolInstanceStateChange>();

        ToolId = toolId;
        SetSerialNumber(serialNumber);
        SetNotes(notes);

        Condition = condition;
        CirculationState = ToolInstanceCirculationState.InCirculation;

        AppendHistory(previousCondition: null, condition, previousCirculationState: null, CirculationState, reason: null, registeredAt, registeredByUserId);
        RaiseStateChangedEvent(previousCondition: null, condition, previousCirculationState: null, CirculationState, reason: null, registeredAt, registeredByUserId);
    }

    /// <summary>Enforces IR-01/IR-02. Catalog-wide uniqueness is re-checked by the caller (<see cref="ToolInstanceManager"/>).</summary>
    public void SetSerialNumber(string serialNumber)
    {
        serialNumber = Check.NotNullOrWhiteSpace(serialNumber, nameof(serialNumber)).Trim();
        Check.Length(
            serialNumber,
            nameof(serialNumber),
            CatalogDomainSharedConsts.SerialNumberMaxLength,
            CatalogDomainSharedConsts.SerialNumberMinLength);

        if (!SerialNumberPattern.IsMatch(serialNumber))
        {
            throw new BusinessException("Catalog:InvalidSerialNumber");
        }

        SerialNumber = serialNumber;
        NormalizedSerialNumber = CatalogTextNormalizer.Normalize(serialNumber);
    }

    public void SetNotes(string? notes)
    {
        notes = notes?.Trim();
        Check.Length(notes, nameof(notes), CatalogDomainSharedConsts.NotesMaxLength);
        Notes = notes;
    }

    /// <summary>
    /// Enforces IR-04: rejected while retired, rejected as a same-value no-op;
    /// otherwise any of the four condition values is permitted in either
    /// direction (a repair can improve condition — this is not a one-way ratchet).
    /// </summary>
    public void ChangeCondition(ToolCondition newCondition, string? reason, DateTime changedAt, Guid? changedByUserId)
    {
        if (CirculationState == ToolInstanceCirculationState.Retired)
        {
            throw new BusinessException("Catalog:InstanceIsRetired");
        }

        if (newCondition == Condition)
        {
            throw new BusinessException("Catalog:ConditionUnchanged");
        }

        reason = reason?.Trim();
        Check.Length(reason, nameof(reason), CatalogDomainSharedConsts.ConditionChangeReasonMaxLength);

        var previousCondition = Condition;
        Condition = newCondition;

        AppendHistory(previousCondition, newCondition, CirculationState, CirculationState, reason, changedAt, changedByUserId);
        RaiseStateChangedEvent(previousCondition, newCondition, CirculationState, CirculationState, reason, changedAt, changedByUserId);
    }

    /// <summary>
    /// Enforces IR-05/IR-06: allowed only from InCirculation, requires a
    /// non-empty reason, and is terminal in this feature (no un-retire).
    /// Never deletes — the record, photos and full history stay retrievable.
    /// </summary>
    public void Retire(string reason, DateTime retiredAt, Guid? retiredByUserId)
    {
        if (CirculationState == ToolInstanceCirculationState.Retired)
        {
            throw new BusinessException("Catalog:InstanceAlreadyRetired");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new BusinessException("Catalog:RetirementReasonRequired");
        }

        reason = reason.Trim();
        Check.Length(reason, nameof(reason), CatalogDomainSharedConsts.RetirementReasonMaxLength);

        var previousCirculationState = CirculationState;
        CirculationState = ToolInstanceCirculationState.Retired;
        RetirementReason = reason;
        RetiredAt = retiredAt;

        AppendHistory(Condition, Condition, previousCirculationState, CirculationState, reason, retiredAt, retiredByUserId);
        RaiseStateChangedEvent(Condition, Condition, previousCirculationState, CirculationState, reason, retiredAt, retiredByUserId);
    }

    /// <summary>
    /// Reports that Lending has checked this instance out (004-lending,
    /// contracts/catalog-extension.md). Permitted only from
    /// <see cref="ToolInstanceCirculationState.InCirculation"/> — Catalog
    /// itself does not know what a loan or a reservation is; it only accepts
    /// this as a fact reported through the inbound
    /// <c>IToolInstanceCirculationReportingAppService</c> contract.
    /// </summary>
    public void MarkOnLoan(DateTime changedAt, Guid? changedByUserId)
    {
        if (CirculationState != ToolInstanceCirculationState.InCirculation)
        {
            throw new BusinessException("Catalog:InstanceNotAvailableForLoan");
        }

        var previousCirculationState = CirculationState;
        CirculationState = ToolInstanceCirculationState.OnLoan;

        AppendHistory(Condition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
        RaiseStateChangedEvent(Condition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
    }

    /// <summary>
    /// Reports a clean return: Lending returns the instance to circulation
    /// with its observed condition. Permitted only from
    /// <see cref="ToolInstanceCirculationState.OnLoan"/>.
    /// </summary>
    public void Return(ToolCondition returnedCondition, DateTime changedAt, Guid? changedByUserId)
    {
        if (CirculationState != ToolInstanceCirculationState.OnLoan)
        {
            throw new BusinessException("Catalog:InstanceNotOnLoan");
        }

        var previousCondition = Condition;
        var previousCirculationState = CirculationState;
        Condition = returnedCondition;
        CirculationState = ToolInstanceCirculationState.InCirculation;

        AppendHistory(previousCondition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
        RaiseStateChangedEvent(previousCondition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
    }

    /// <summary>
    /// Reports a worsened return: Lending returns the instance directly to
    /// <see cref="ToolInstanceCirculationState.UnderMaintenance"/> rather than
    /// back to circulation, recording the worsened condition observed.
    /// Permitted only from <see cref="ToolInstanceCirculationState.OnLoan"/>.
    /// </summary>
    public void ReturnForMaintenance(ToolCondition returnedCondition, DateTime changedAt, Guid? changedByUserId)
    {
        if (CirculationState != ToolInstanceCirculationState.OnLoan)
        {
            throw new BusinessException("Catalog:InstanceNotOnLoan");
        }

        var previousCondition = Condition;
        var previousCirculationState = CirculationState;
        Condition = returnedCondition;
        CirculationState = ToolInstanceCirculationState.UnderMaintenance;

        AppendHistory(previousCondition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
        RaiseStateChangedEvent(previousCondition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
    }

    /// <summary>
    /// Reports that a problem was found on an instance sitting in circulation
    /// (008-out-of-band-maintenance, IR-09): Lending sends it directly to
    /// <see cref="ToolInstanceCirculationState.UnderMaintenance"/>, recording
    /// the observed condition if it is worse. The observed condition may equal
    /// the current one — a safety defect need not move the tool down the
    /// scale — but never improve on it. One history row carries both the
    /// circulation change and <paramref name="reason"/>.
    /// </summary>
    public void SendToMaintenance(ToolCondition observedCondition, string reason, DateTime changedAt, Guid? changedByUserId)
    {
        if (CirculationState == ToolInstanceCirculationState.Retired)
        {
            throw new BusinessException("Catalog:InstanceIsRetired");
        }

        if (CirculationState != ToolInstanceCirculationState.InCirculation)
        {
            throw new BusinessException("Catalog:InstanceNotAvailableForMaintenance");
        }

        if ((int)observedCondition < (int)Condition)
        {
            throw new BusinessException("Catalog:ObservedConditionBetterThanCurrent");
        }

        reason = Check.NotNullOrWhiteSpace(reason, nameof(reason)).Trim();
        Check.Length(reason, nameof(reason), CatalogDomainSharedConsts.ConditionChangeReasonMaxLength);

        var previousCondition = Condition;
        var previousCirculationState = CirculationState;
        Condition = observedCondition;
        CirculationState = ToolInstanceCirculationState.UnderMaintenance;

        AppendHistory(previousCondition, Condition, previousCirculationState, CirculationState, reason, changedAt, changedByUserId);
        RaiseStateChangedEvent(previousCondition, Condition, previousCirculationState, CirculationState, reason, changedAt, changedByUserId);
    }

    /// <summary>
    /// Reports that Lending's maintenance request against this instance has
    /// closed. Permitted only from <see cref="ToolInstanceCirculationState.UnderMaintenance"/>.
    /// <see cref="Condition"/> is left untouched — this feature does not model
    /// a condition change at the moment a request closes.
    /// </summary>
    public void CloseMaintenance(DateTime changedAt, Guid? changedByUserId)
    {
        if (CirculationState != ToolInstanceCirculationState.UnderMaintenance)
        {
            throw new BusinessException("Catalog:InstanceNotUnderMaintenance");
        }

        var previousCirculationState = CirculationState;
        CirculationState = ToolInstanceCirculationState.InCirculation;

        AppendHistory(Condition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
        RaiseStateChangedEvent(Condition, Condition, previousCirculationState, CirculationState, reason: null, changedAt, changedByUserId);
    }

    /// <summary>Enforces IR-08: rejects once <paramref name="maxPhotos"/> is reached; the first photo becomes primary.</summary>
    public ToolInstancePhoto AddPhoto(
        string blobName,
        string fileName,
        string contentType,
        long sizeBytes,
        IEnumerable<string> allowedContentTypes,
        long maxSizeBytes,
        int maxPhotos)
    {
        if (!allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException("Catalog:UnsupportedPhotoFormat").WithData("contentType", contentType);
        }

        if (sizeBytes <= 0 || sizeBytes > maxSizeBytes)
        {
            throw new BusinessException("Catalog:PhotoTooLarge").WithData("maxSizeBytes", maxSizeBytes);
        }

        if (Photos.Count >= maxPhotos)
        {
            throw new BusinessException("Catalog:TooManyPhotos").WithData("maxPhotos", maxPhotos);
        }

        var isPrimary = Photos.Count == 0;
        var displayOrder = Photos.Count == 0 ? 0 : Photos.Max(p => p.DisplayOrder) + 1;

        var photo = new ToolInstancePhoto(Guid.NewGuid(), Id, blobName, fileName, contentType, sizeBytes, displayOrder, isPrimary);
        Photos.Add(photo);
        return photo;
    }

    /// <summary>Removes a photo; if it was primary, promotes the next by <see cref="ToolInstancePhoto.DisplayOrder"/>.</summary>
    public void RemovePhoto(Guid photoId)
    {
        var photo = Photos.FirstOrDefault(p => p.Id == photoId);
        if (photo is null)
        {
            return;
        }

        var wasPrimary = photo.IsPrimary;
        Photos.Remove(photo);

        if (wasPrimary)
        {
            var next = Photos.OrderBy(p => p.DisplayOrder).FirstOrDefault();
            if (next is not null)
            {
                next.IsPrimary = true;
            }
        }
    }

    public void SetPrimaryPhoto(Guid photoId)
    {
        var target = Photos.FirstOrDefault(p => p.Id == photoId);
        if (target is null)
        {
            return;
        }

        foreach (var photo in Photos)
        {
            photo.IsPrimary = photo.Id == photoId;
        }
    }

    private void AppendHistory(
        ToolCondition? previousCondition,
        ToolCondition newCondition,
        ToolInstanceCirculationState? previousCirculationState,
        ToolInstanceCirculationState newCirculationState,
        string? reason,
        DateTime changedAt,
        Guid? changedByUserId)
    {
        StateHistory.Add(new ToolInstanceStateChange(
            Guid.NewGuid(),
            Id,
            previousCondition,
            newCondition,
            previousCirculationState,
            newCirculationState,
            reason,
            changedAt,
            changedByUserId));
    }

    private void RaiseStateChangedEvent(
        ToolCondition? previousCondition,
        ToolCondition newCondition,
        ToolInstanceCirculationState? previousCirculationState,
        ToolInstanceCirculationState newCirculationState,
        string? reason,
        DateTime changedAt,
        Guid? changedByUserId)
    {
        AddLocalEvent(new ToolInstanceStateChangedEto
        {
            ToolInstanceId = Id,
            ToolId = ToolId,
            SerialNumber = SerialNumber,
            PreviousCondition = previousCondition,
            NewCondition = newCondition,
            PreviousCirculationState = previousCirculationState,
            NewCirculationState = newCirculationState,
            Reason = reason,
            ChangedAt = changedAt,
            ChangedByUserId = changedByUserId
        });
    }
}
