using Shouldly;
using Xunit;

namespace ToolShare.Membership;

public class CommunityRoleHierarchyTests
{
    [Fact]
    public void Administrator_outranks_Librarian_and_Member()
    {
        (CommunityRole.Administrator >= CommunityRole.Librarian).ShouldBeTrue();
        (CommunityRole.Administrator >= CommunityRole.Member).ShouldBeTrue();
    }

    [Fact]
    public void Librarian_outranks_itself_and_Member_but_not_Administrator()
    {
        var librarian = CommunityRole.Librarian;

        (librarian >= CommunityRole.Librarian).ShouldBeTrue();
        (librarian >= CommunityRole.Member).ShouldBeTrue();
        (librarian >= CommunityRole.Administrator).ShouldBeFalse();
    }

    [Fact]
    public void Member_does_not_outrank_Librarian_or_Administrator()
    {
        (CommunityRole.Member >= CommunityRole.Librarian).ShouldBeFalse();
        (CommunityRole.Member >= CommunityRole.Administrator).ShouldBeFalse();
    }

    [Fact]
    public void Numeric_ordering_matches_the_published_contract()
    {
        ((int)CommunityRole.Member).ShouldBe(0);
        ((int)CommunityRole.Librarian).ShouldBe(1);
        ((int)CommunityRole.Administrator).ShouldBe(2);
    }
}
