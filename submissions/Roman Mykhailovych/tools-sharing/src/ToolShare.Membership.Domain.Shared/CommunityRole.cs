namespace ToolShare.Membership;

/// <summary>
/// Membership's single hierarchical role. Hierarchical by <b>numeric order</b>:
/// <c>role >= CommunityRole.Librarian</c> is the capability test (FR-004). The
/// numeric ordering is part of the public contract — values must never be
/// renumbered; new levels, if any, are appended above <see cref="Administrator"/>.
/// </summary>
public enum CommunityRole
{
    /// <summary>Browse and borrow.</summary>
    Member = 0,

    /// <summary>Everything <see cref="Member"/> has, plus catalog management and member viewing.</summary>
    Librarian = 1,

    /// <summary>Everything <see cref="Librarian"/> has, plus roster and rules administration.</summary>
    Administrator = 2
}
