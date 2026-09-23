using System;
using System.Threading.Tasks;

namespace ToolShare.Membership.Members;

/// <summary>
/// The inward-pointing port <see cref="MemberAppService"/> orchestrates enrolment
/// through (research R2). Declared here — in <c>Application.Contracts</c>, not
/// <c>Domain</c> — because <c>Domain</c> may not reference <c>Application.Contracts</c>
/// and because the host is already permitted to reference another module's
/// <c>Application.Contracts</c> (002 does exactly this for <c>CatalogPermissions</c>),
/// so this introduces no new coupling direction. The implementation
/// (<c>IdentityMemberIdentityProvisioner</c>) lives in the host
/// (<c>ToolShare.Application/Identity/</c>) and is the only place that touches
/// <c>Volo.Abp.Identity</c> on Membership's behalf — Membership itself must never
/// reference the Identity module directly.
/// </summary>
public interface IMemberIdentityProvisioner
{
    /// <summary>
    /// Creates the sign-in account for a new member, forcing a password change on
    /// first sign-in (FR-001a). Identity-store validation failures (duplicate
    /// email, weak password) surface unwrapped so the message names the real
    /// conflict (FR-002) — callers should not catch and re-wrap them.
    /// </summary>
    Task<Guid> CreateAsync(string displayName, string email, string initialPassword);

    /// <summary>
    /// Replaces the identity account's ABP role set with exactly the single
    /// named role (research R5) — never merges, so a role change can never leave
    /// a user holding two roles.
    /// </summary>
    Task SetRoleAsync(Guid identityUserId, string roleName);
}
