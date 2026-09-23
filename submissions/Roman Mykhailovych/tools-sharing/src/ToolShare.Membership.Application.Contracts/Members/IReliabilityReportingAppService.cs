using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ToolShare.Membership.Members;

/// <summary>
/// The one inbound operation on the public boundary (US5, Tier 1): how Lending
/// tells Membership that something rating-affecting happened. Idempotent on
/// <c>(MemberId's outcome for a given OccurrenceId, OutcomeType)</c> and safe
/// under concurrent reporting for the same member (FR-032) — see
/// <see cref="Members.ReliabilityReportingAppService"/>.
/// </summary>
public interface IReliabilityReportingAppService : IApplicationService
{
    Task<ReliabilityReportResultDto> ReportAsync(ReportReliabilityOutcomeDto input);
}

public class ReportReliabilityOutcomeDto
{
    [Required]
    public Guid MemberId { get; set; }

    public ReliabilityOutcomeType OutcomeType { get; set; }

    /// <summary>
    /// Identifier of the thing in the calling module that caused this outcome
    /// (a loan, a return). Required — it is the idempotency key, combined
    /// with <see cref="OutcomeType"/> (one loan may legitimately produce both
    /// an <see cref="ReliabilityOutcomeType.OverdueReturn"/> and a
    /// <see cref="ReliabilityOutcomeType.DamagedReturn"/> outcome).
    /// </summary>
    [Required]
    public Guid OccurrenceId { get; set; }

    [StringLength(512)]
    public string? Note { get; set; }
}

public class ReliabilityReportResultDto
{
    public bool Applied { get; set; }

    /// <summary>True when this exact <c>(OccurrenceId, OutcomeType)</c> was already recorded — a retrying caller must be able to treat this as success (FR-019, SC-009).</summary>
    public bool AlreadyRecorded { get; set; }

    public int RawPoints { get; set; }

    public int EffectivePoints { get; set; }

    public int ResultingRating { get; set; }
}
