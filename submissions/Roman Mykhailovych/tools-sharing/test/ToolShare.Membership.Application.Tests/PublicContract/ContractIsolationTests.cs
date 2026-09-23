using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ToolShare.Membership.Members;

/// <summary>
/// Enforces SC-011 by scanning the source of every file in this same folder:
/// none may import a namespace exclusive to Membership's Domain or
/// EntityFrameworkCore layer, nor reference one of their concrete internal
/// types by bare name. The absent import is the assertion — mirrors Catalog's
/// own <c>ContractIsolationTests</c> (002, SC-007).
///
/// Namespace-only scanning is not enough here, for the same reason it wasn't
/// enough for Catalog: this codebase organizes Domain/Application.Contracts/
/// Application by *feature slice*, so e.g. the <c>Member</c> entity (Domain)
/// and <c>MemberDetailDto</c> (Application.Contracts) legitimately share the
/// namespace <c>ToolShare.Membership.Members</c>. One additional wrinkle
/// beyond Catalog's precedent: the <c>CommunityRules</c> aggregate root
/// (Domain) and its own feature-slice namespace <c>ToolShare.Membership.CommunityRules</c>
/// are spelled identically, so every legitimate qualified reference —
/// including this project's own <c>namespace ToolShare.Membership.CommunityRules;</c>
/// declarations — has "CommunityRules" immediately preceded by a dot. The
/// bare-identifier regex below requires a *non*-dot-preceded match, which is
/// exactly what distinguishes "the namespace" from "the bare entity type".
/// </summary>
public class ContractIsolationTests
{
    /// <summary>Namespaces that exist only in the EntityFrameworkCore project (never part of the published boundary).</summary>
    private static readonly string[] ForbiddenNamespacePrefixes =
    {
        "ToolShare.Membership.EntityFrameworkCore",
        "ToolShare.Membership.Repositories",
        "ToolShare.Membership.Migrations"
    };

    /// <summary>
    /// Concrete Domain/EntityFrameworkCore type names, matched as whole
    /// identifiers not immediately preceded by a dot — so a qualified
    /// reference or namespace segment (always dot-preceded) never falsely
    /// matches, only a genuine bare/unqualified use of the Domain type does.
    /// </summary>
    private static readonly string[] ForbiddenBareIdentifiers =
    {
        "MembershipDbContext",
        "IMembershipDbContext",
        "MembershipDbProperties",
        "MembershipDbContextFactory",
        "EfCoreMemberRepository",
        "EfCoreCommunityRulesRepository",
        "IMemberRepository",
        "ICommunityRulesRepository",
        "MemberManager",
        "MembershipDataSeedContributor",
        "ReliabilityPolicy",
        "MemberStandingChange",
        "Member",
        "CommunityRules"
    };

    [Fact]
    public void PublicContract_test_files_do_not_import_Membership_Domain_or_EntityFrameworkCore_namespaces()
    {
        var violations = new List<string>();

        foreach (var file in GetPublicContractSourceFiles())
        {
            var text = File.ReadAllText(file);
            var usings = Regex.Matches(text, @"^\s*using\s+([A-Za-z0-9_.]+)\s*;", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value);

            foreach (var ns in usings)
            {
                if (ForbiddenNamespacePrefixes.Any(forbidden => ns == forbidden || ns.StartsWith(forbidden + ".", StringComparison.Ordinal)))
                {
                    violations.Add($"{Path.GetFileName(file)}: forbidden 'using {ns};'");
                }
            }
        }

        violations.ShouldBeEmpty(violations.Count == 0 ? string.Empty : string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PublicContract_test_files_do_not_reference_Membership_Domain_types_by_bare_name()
    {
        var violations = new List<string>();

        foreach (var file in GetPublicContractSourceFiles())
        {
            var text = StripCommentsAndStrings(File.ReadAllText(file));

            foreach (var identifier in ForbiddenBareIdentifiers)
            {
                if (Regex.IsMatch(text, $@"(?<!\.)\b{Regex.Escape(identifier)}\b"))
                {
                    violations.Add($"{Path.GetFileName(file)}: forbidden bare reference to Domain type '{identifier}'");
                }
            }
        }

        violations.ShouldBeEmpty(violations.Count == 0 ? string.Empty : string.Join(Environment.NewLine, violations));
    }

    /// <summary>Removes // line comments, /* */ block comments, and string/char literal contents so prose (e.g. XML doc comments) can't trigger false positives.</summary>
    private static string StripCommentsAndStrings(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", m => new string(' ', m.Length), RegexOptions.Singleline);
        source = Regex.Replace(source, @"//.*$", m => new string(' ', m.Length), RegexOptions.Multiline);
        source = Regex.Replace(source, "\"(?:[^\"\\\\]|\\\\.)*\"", m => new string(' ', m.Length));
        return source;
    }

    private static IEnumerable<string> GetPublicContractSourceFiles()
    {
        var directory = FindPublicContractDirectory();
        return Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
    }

    private static string FindPublicContractDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ToolShare.slnx")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException("Could not locate the repository root (ToolShare.slnx) from " + AppContext.BaseDirectory);
        }

        var publicContractDir = Path.Combine(current.FullName, "test", "ToolShare.Membership.Application.Tests", "PublicContract");
        if (!Directory.Exists(publicContractDir))
        {
            throw new InvalidOperationException("PublicContract test directory not found at " + publicContractDir);
        }

        return publicContractDir;
    }
}
