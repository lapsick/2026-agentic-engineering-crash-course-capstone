using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ToolShare.Catalog.ToolInstances;
using ToolShare.Lending.Maintenance;
using ToolShare.Membership.CommunityRules;
using ToolShare.Membership.Members;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;

namespace ToolShare.Lending.Reservations;

/// <summary>
/// Reservations and the waitlist (US1). Creating a reservation reads
/// Catalog's availability and Membership's standing/rules live — Lending
/// caches none of it in its own schema (research, membership-consumption.md).
/// </summary>
[Authorize]
public class ReservationAppService : ApplicationService, IReservationAppService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IWaitlistEntryRepository _waitlistEntryRepository;
    private readonly ReservationManager _reservationManager;
    private readonly WaitlistManager _waitlistManager;
    private readonly IToolInstanceLookupAppService _toolInstanceLookupAppService;
    private readonly IMemberStandingAppService _memberStandingAppService;
    private readonly ICommunityRulesLookupAppService _communityRulesLookupAppService;
    private readonly IInstanceLock _instanceLock;

    public ReservationAppService(
        IReservationRepository reservationRepository,
        IWaitlistEntryRepository waitlistEntryRepository,
        ReservationManager reservationManager,
        WaitlistManager waitlistManager,
        IToolInstanceLookupAppService toolInstanceLookupAppService,
        IMemberStandingAppService memberStandingAppService,
        ICommunityRulesLookupAppService communityRulesLookupAppService,
        IInstanceLock instanceLock)
    {
        _instanceLock = instanceLock;
        _reservationRepository = reservationRepository;
        _waitlistEntryRepository = waitlistEntryRepository;
        _reservationManager = reservationManager;
        _waitlistManager = waitlistManager;
        _toolInstanceLookupAppService = toolInstanceLookupAppService;
        _memberStandingAppService = memberStandingAppService;
        _communityRulesLookupAppService = communityRulesLookupAppService;
    }

    public virtual async Task<ReservationDto> CreateAsync(CreateReservationDto input)
    {
        var standing = await GetOwnStandingOrThrowAsync();
        var rules = await _communityRulesLookupAppService.GetAsync();

        // 008 research R4: serialize with an out-of-band maintenance report on
        // the same instance *before* reading availability, so a reservation can
        // never commit after that report's cancellation sweep and survive it.
        await _instanceLock.LockInstanceAsync(input.ToolInstanceId);
        var isAvailable = await _toolInstanceLookupAppService.IsAvailableAsync(input.ToolInstanceId);

        var reservation = await _reservationManager.CreateAsync(
            standing.MemberId!.Value,
            input.ToolInstanceId,
            input.StartDate,
            input.EndDate,
            rules.MaxLoanTermDays,
            standing.EffectiveConcurrentLoanLimit,
            isAvailable,
            Clock.Now);

        await _reservationRepository.InsertAsync(reservation, autoSave: true);

        return MapToDto(reservation);
    }

    public virtual async Task CancelAsync(Guid id)
    {
        var standing = await GetOwnStandingOrThrowAsync();
        var reservation = await _reservationRepository.GetAsync(id);

        if (reservation.MemberId != standing.MemberId!.Value)
        {
            throw new AbpAuthorizationException();
        }

        reservation.Cancel(Clock.Now);
        await _reservationRepository.UpdateAsync(reservation, autoSave: true);

        var rules = await _communityRulesLookupAppService.GetAsync();
        await _waitlistManager.OfferNextAsync(reservation.ToolInstanceId, Clock.Now, rules.WaitlistOfferWindowHours);
    }

    public virtual async Task<WaitlistEntryDto> JoinWaitlistAsync(JoinWaitlistDto input)
    {
        var standing = await GetOwnStandingOrThrowAsync();

        var entry = await _waitlistManager.JoinAsync(standing.MemberId!.Value, input.ToolInstanceId, Clock.Now);
        await _waitlistEntryRepository.InsertAsync(entry, autoSave: true);

        return MapToDto(entry);
    }

    public virtual async Task<PagedResultDto<ReservationDto>> GetListForInstanceAsync(Guid toolInstanceId)
    {
        var queryable = await _reservationRepository.GetQueryableAsync();
        var reservations = await AsyncExecuter.ToListAsync(queryable.Where(r => r.ToolInstanceId == toolInstanceId));

        return new PagedResultDto<ReservationDto>(reservations.Count, reservations.Select(MapToDto).ToList());
    }

    private async Task<MemberStandingDto> GetOwnStandingOrThrowAsync()
    {
        var identityUserId = CurrentUser.Id
            ?? throw new BusinessException(LendingDomainErrorCodes.InvalidStateTransition);

        var standing = await _memberStandingAppService.GetByIdentityUserIdAsync(identityUserId);
        if (!standing.IsEnrolled || !standing.IsActive)
        {
            // Defensive only — the enrolment gate already refuses this
            // request before this line is ever reached (FR-018/FR-019).
            throw new AbpAuthorizationException();
        }

        return standing;
    }

    private static ReservationDto MapToDto(Reservation reservation) => new()
    {
        Id = reservation.Id,
        MemberId = reservation.MemberId,
        ToolInstanceId = reservation.ToolInstanceId,
        StartDate = reservation.StartDate,
        EndDate = reservation.EndDate,
        Status = reservation.Status,
        CreatedAt = reservation.CreatedAt,
        CancelledAt = reservation.CancelledAt,
        CancellationReason = reservation.CancellationReason
    };

    private static WaitlistEntryDto MapToDto(WaitlistEntry entry) => new()
    {
        Id = entry.Id,
        MemberId = entry.MemberId,
        ToolInstanceId = entry.ToolInstanceId,
        JoinedAt = entry.JoinedAt,
        OfferState = entry.OfferState,
        OfferExpiresAt = entry.OfferExpiresAt
    };
}
