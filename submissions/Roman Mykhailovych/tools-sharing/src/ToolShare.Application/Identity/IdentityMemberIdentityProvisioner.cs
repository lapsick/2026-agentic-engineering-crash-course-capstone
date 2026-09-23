using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using ToolShare.Membership.Members;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace ToolShare.Identity;

/// <summary>
/// The only place that touches <c>IdentityUserManager</c>/<c>IdentityRoleManager</c>
/// on Membership's behalf (research R2). Implements the port Membership declares
/// (<see cref="IMemberIdentityProvisioner"/>) so Membership itself never
/// references <c>Volo.Abp.Identity</c> — the same "host touches Identity for a
/// business module" precedent <see cref="LibrarianRoleDataSeedContributor"/>
/// established for permission seeding.
/// </summary>
public class IdentityMemberIdentityProvisioner : IMemberIdentityProvisioner, ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IIdentityRoleRepository _roleRepository;
    private readonly IGuidGenerator _guidGenerator;

    public IdentityMemberIdentityProvisioner(
        IdentityUserManager userManager,
        IIdentityRoleRepository roleRepository,
        IGuidGenerator guidGenerator)
    {
        _userManager = userManager;
        _roleRepository = roleRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>
    /// Creates the sign-in account and forces a password change on first sign-in
    /// (FR-001a, research R4) via ABP's native
    /// <see cref="IdentityUser.SetShouldChangePasswordOnNextLogin"/>. Identity-store
    /// validation failures (duplicate email, weak password) are surfaced verbatim
    /// via <c>CheckErrors()</c>, which throws <c>AbpIdentityResultException</c>
    /// naming the real conflict (FR-002) — deliberately not caught here.
    /// </summary>
    public virtual async Task<Guid> CreateAsync(string displayName, string email, string initialPassword)
    {
        var userName = await _userManager.GetUserNameFromEmailAsync(email);

        var user = new IdentityUser(_guidGenerator.Create(), userName, email)
        {
            Name = displayName
        };
        user.SetShouldChangePasswordOnNextLogin(true);

        (await _userManager.CreateAsync(user, initialPassword)).CheckErrors();

        return user.Id;
    }

    /// <summary>
    /// Replaces the identity account's role set with exactly <paramref name="roleName"/>
    /// (research R5) — <see cref="IdentityUserManager.SetRolesAsync"/> replaces the
    /// full role list rather than adding to it, so a role change can never leave a
    /// user holding two roles.
    /// </summary>
    public virtual async Task SetRoleAsync(Guid identityUserId, string roleName)
    {
        var user = await _userManager.GetByIdAsync(identityUserId);

        if (await _roleRepository.FindByNormalizedNameAsync(roleName.ToUpperInvariant()) is null)
        {
            throw new InvalidOperationException($"The '{roleName}' ABP role does not exist — the host role seeder must run before enrolment.");
        }

        (await _userManager.SetRolesAsync(user, new List<string> { roleName })).CheckErrors();
    }
}
