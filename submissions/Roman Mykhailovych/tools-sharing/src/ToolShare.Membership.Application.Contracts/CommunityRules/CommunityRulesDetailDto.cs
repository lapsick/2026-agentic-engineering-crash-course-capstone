namespace ToolShare.Membership.CommunityRules;

/// <summary>
/// The community rules plus edit metadata (FR-015: "last changed by/at").
/// Extends the Tier 1 public <see cref="CommunityRulesDto"/> rather than
/// duplicating its fields, so a rule added later cannot be added to one and
/// forgotten on the other (US5, T105 — this type previously declared the rule
/// fields directly, ahead of the public DTO's existence; now a pure refactor).
/// </summary>
public class CommunityRulesDetailDto : CommunityRulesDto
{
    /// <summary>
    /// Resolved from the Membership roster (the actor is always a member),
    /// never from the identity store — Membership must not reference
    /// Identity. Null on a fresh installation or if the editor's member
    /// record can no longer be found.
    /// </summary>
    public string? LastChangedByDisplayName { get; set; }

    public string ConcurrencyStamp { get; set; } = default!;
}
