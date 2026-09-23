using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ToolShare.Lending.Reservations;

/// <summary>WL-03, WL-04, WL-05 — pure entity guards, no database.</summary>
public class WaitlistEntryLifecycleTests
{
    private static readonly DateTime JoinedAt = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Offer_is_permitted_only_while_Waiting()
    {
        var entry = new WaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), JoinedAt);

        entry.Offer(JoinedAt.AddDays(1), windowHours: 24);

        entry.OfferState.ShouldBe(WaitlistOfferState.Offered);
        entry.OfferedAt.ShouldBe(JoinedAt.AddDays(1));
        entry.OfferExpiresAt.ShouldBe(JoinedAt.AddDays(1).AddHours(24));

        Should.Throw<BusinessException>(() => entry.Offer(JoinedAt.AddDays(2), 24));
    }

    [Fact]
    public void Confirm_is_permitted_only_while_Offered_and_within_the_window()
    {
        var entry = new WaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), JoinedAt);
        var offeredAt = JoinedAt.AddDays(1);
        entry.Offer(offeredAt, windowHours: 24);

        var reservationId = Guid.NewGuid();
        entry.Confirm(reservationId, offeredAt.AddHours(12));

        entry.OfferState.ShouldBe(WaitlistOfferState.Confirmed);
        entry.RealizedReservationId.ShouldBe(reservationId);
    }

    [Fact]
    public void Confirm_past_the_window_throws()
    {
        var entry = new WaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), JoinedAt);
        var offeredAt = JoinedAt.AddDays(1);
        entry.Offer(offeredAt, windowHours: 24);

        Should.Throw<BusinessException>(() => entry.Confirm(Guid.NewGuid(), offeredAt.AddHours(25)));
    }

    [Fact]
    public void Confirm_while_still_Waiting_throws()
    {
        var entry = new WaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), JoinedAt);

        Should.Throw<BusinessException>(() => entry.Confirm(Guid.NewGuid(), JoinedAt));
    }

    [Fact]
    public void Expire_is_permitted_only_while_Offered_and_past_the_window()
    {
        var entry = new WaitlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), JoinedAt);
        var offeredAt = JoinedAt.AddDays(1);
        entry.Offer(offeredAt, windowHours: 24);

        Should.Throw<BusinessException>(() => entry.Expire(offeredAt.AddHours(1)));

        entry.Expire(offeredAt.AddHours(25));
        entry.OfferState.ShouldBe(WaitlistOfferState.Expired);
    }
}
