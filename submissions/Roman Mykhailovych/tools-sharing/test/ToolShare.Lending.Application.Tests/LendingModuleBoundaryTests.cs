using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ToolShare.Lending;

/// <summary>
/// Constitution Principle II: Lending's own production code
/// (<c>src/ToolShare.Lending.Application</c>, <c>src/ToolShare.Lending.Domain</c>)
/// must never reference Catalog's or Membership's Domain or
/// EntityFrameworkCore layer — only their published <c>Application.Contracts</c>.
/// This is the reverse-direction check 002/003 never needed (they were only
/// producers of a public contract; Lending is the first module to be a heavy
/// *consumer* of two others' contracts).
///
/// Namespace-prefix banning alone is not enough: Catalog and Membership both
/// organize Domain and Application.Contracts by feature slice, so e.g.
/// <c>ToolShare.Membership.CommunityRules</c> is legitimately both the
/// <c>CommunityRules</c> Domain aggregate's namespace AND
/// <c>ICommunityRulesLookupAppService</c>/<c>CommunityRulesDto</c>'s namespace.
/// Namespace-prefix bans are used only for namespaces exclusive to the
/// EntityFrameworkCore layer (never shared with Contracts); everything else is
/// enforced by bare-identifier matching (a qualified/namespace reference is
/// always dot-preceded, so the negative lookbehind below only ever flags a
/// genuine unqualified use of a forbidden Domain type) — mirrors Membership's
/// own <c>ContractIsolationTests</c> (003, SC-011).
/// </summary>
public class LendingModuleBoundaryTests
{
    private static readonly string[] ForbiddenNamespacePrefixes =
    {
        "ToolShare.Catalog.EntityFrameworkCore",
        "ToolShare.Catalog.Repositories",
        "ToolShare.Catalog.Migrations",
        "ToolShare.Membership.EntityFrameworkCore",
        "ToolShare.Membership.Repositories",
        "ToolShare.Membership.Migrations"
    };

    private static readonly string[] ForbiddenBareIdentifiers =
    {
        // Catalog Domain — aggregate roots/entities, repository interfaces, domain services
        "Tool",
        "Category",
        "ToolInstance",
        "ToolInstancePhoto",
        "ToolInstanceStateChange",
        "ToolInstanceLookupRow",
        "IToolRepository",
        "ICategoryRepository",
        "IToolInstanceRepository",
        "ToolInstanceManager",
        "CategoryManager",
        "CatalogDataSeedContributor",

        // Membership Domain — aggregate roots/entities, repository interfaces, domain services
        "Member",
        "CommunityRules",
        "MemberManager",
        "ReliabilityPolicy",
        "MemberStandingChange",
        "IMemberRepository",
        "ICommunityRulesRepository",
        "MembershipDataSeedContributor"
    };

    [Fact]
    public void Lending_production_code_does_not_import_Catalog_or_Membership_EntityFrameworkCore_namespaces()
    {
        var violations = new List<string>();

        foreach (var file in GetLendingProductionSourceFiles())
        {
            var text = File.ReadAllText(file);
            var usings = Regex.Matches(text, @"^\s*using\s+([A-Za-z0-9_.]+)\s*;", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value);

            foreach (var ns in usings)
            {
                if (ForbiddenNamespacePrefixes.Any(forbidden => ns == forbidden || ns.StartsWith(forbidden + ".", StringComparison.Ordinal)))
                {
                    violations.Add($"{file}: forbidden 'using {ns};'");
                }
            }
        }

        violations.ShouldBeEmpty(violations.Count == 0 ? string.Empty : string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Lending_production_code_does_not_reference_Catalog_or_Membership_Domain_types_by_bare_name()
    {
        var violations = new List<string>();

        foreach (var file in GetLendingProductionSourceFiles())
        {
            var text = StripCommentsAndStrings(File.ReadAllText(file));

            foreach (var identifier in ForbiddenBareIdentifiers)
            {
                if (Regex.IsMatch(text, $@"(?<!\.)\b{Regex.Escape(identifier)}\b"))
                {
                    violations.Add($"{file}: forbidden bare reference to Domain type '{identifier}'");
                }
            }
        }

        violations.ShouldBeEmpty(violations.Count == 0 ? string.Empty : string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// 008 research R8: Catalog's UI reaches Lending's pages (Reserve, Report
    /// damage) by URL only — it must never take a project reference on, or
    /// import a namespace of, any Lending project (Constitution II).
    /// </summary>
    [Fact]
    public void Catalog_Blazor_does_not_reference_or_import_any_Lending_project()
    {
        var catalogBlazor = Path.Combine(FindRepositoryRoot(), "src", "ToolShare.Catalog.Blazor");
        var violations = new List<string>();

        foreach (var csproj in Directory.GetFiles(catalogBlazor, "*.csproj"))
        {
            if (Regex.IsMatch(File.ReadAllText(csproj), @"ProjectReference\s+Include=""[^""]*ToolShare\.Lending\."))
            {
                violations.Add($"{csproj}: project reference to a Lending project");
            }
        }

        var sources = Directory.GetFiles(catalogBlazor, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(catalogBlazor, "*.razor", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var file in sources)
        {
            if (Regex.IsMatch(File.ReadAllText(file), @"^\s*@?using\s+ToolShare\.Lending\b", RegexOptions.Multiline))
            {
                violations.Add($"{file}: imports a ToolShare.Lending namespace");
            }
        }

        violations.ShouldBeEmpty(violations.Count == 0 ? string.Empty : string.Join(Environment.NewLine, violations));
    }

    private static string StripCommentsAndStrings(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", m => new string(' ', m.Length), RegexOptions.Singleline);
        source = Regex.Replace(source, @"//.*$", m => new string(' ', m.Length), RegexOptions.Multiline);
        source = Regex.Replace(source, "\"(?:[^\"\\\\]|\\\\.)*\"", m => new string(' ', m.Length));
        return source;
    }

    private static IEnumerable<string> GetLendingProductionSourceFiles()
    {
        var root = FindRepositoryRoot();

        var directories = new[]
        {
            Path.Combine(root, "src", "ToolShare.Lending.Application"),
            Path.Combine(root, "src", "ToolShare.Lending.Domain")
        };

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException($"Expected directory not found: {directory}");
            }

            foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static string FindRepositoryRoot()
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

        return current.FullName;
    }
}
