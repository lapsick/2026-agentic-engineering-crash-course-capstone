using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// A member's queued place in line for an instance that was unavailable when
/// they wanted it (data-model.md `WL-01`–`WL-05`). Ordering is
/// <see cref="JoinedAt"/> ascending, fixed once at insert (`WL-01`).
/// </summary>
public class WaitlistEntry : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid MemberId { get; private set; }

    public virtual Guid ToolInstanceId { get; private set; }

    public virtual DateTime JoinedAt { get; private set; }

    public virtual WaitlistOfferState OfferState { get; private set; }

    public virtual DateTime? OfferedAt { get; private set; }

    public virtual DateTime? OfferExpiresAt { get; private set; }

    public virtual DateTime? ResolvedAt { get; private set; }

    public virtual Guid? RealizedReservationId { get; private set; }

    protected WaitlistEntry()
    {
    }

    /// <summary>Duplicate-entry prevention (WL-02) needs repository access and is enforced by <see cref="WaitlistManager"/>, not here.</summary>
    public WaitlistEntry(Guid id, Guid memberId, Guid toolInstanceId, DateTime joinedAt)
        : base(id)
    {
        MemberId = memberId;
        ToolInstanceId = toolInstanceId;
        JoinedAt = joinedAt;
        OfferState = WaitlistOfferState.Waiting;
    }

    /// <summary>Enforces WL-03: permitted only while <see cref="WaitlistOfferState.Waiting"/>. Which entry is "earliest" is <see cref="WaitlistManager"/>'s concern.</summary>
    public void Offer(DateTime at, int windowHours)
    {
        if (OfferState != WaitlistOfferState.Waiting)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        OfferState = WaitlistOfferState.Offered;
        OfferedAt = at;
        OfferExpiresAt = at.AddHours(windowHours);
    }

    /// <summary>Enforces WL-04: permitted only while <see cref="WaitlistOfferState.Offered"/> and within the window.</summary>
    public void Confirm(Guid reservationId, DateTime at)
    {
        if (OfferState != WaitlistOfferState.Offered)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        if (at > OfferExpiresAt)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        OfferState = WaitlistOfferState.Confirmed;
        ResolvedAt = at;
        RealizedReservationId = reservationId;
    }

    /// <summary>Enforces WL-05: permitted only while <see cref="WaitlistOfferState.Offered"/> and past the window. Triggers the next offer (research R4) at the application layer, not here.</summary>
    public void Expire(DateTime at)
    {
        if (OfferState != WaitlistOfferState.Offered)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        if (at <= OfferExpiresAt)
        {
            throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);
        }

        OfferState = WaitlistOfferState.Expired;
        ResolvedAt = at;
    }
}
