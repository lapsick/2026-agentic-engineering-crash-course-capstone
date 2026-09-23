using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Enforces SC-007 by scanning the source of every file in this same folder:
/// none may import a namespace exclusive to the Catalog module's Domain or
/// EntityFrameworkCore layer, nor reference one of their concrete internal
/// types by bare name (never qualified by a suffix like "Dto"/"AppService",
/// which would make it a Tier 1/Tier 2 contract type instead). The absent
/// import is the assertion — see contracts/README.md's "How SC-007 is
/// verified" and CLAUDE.md's "Stability tiers" section.
///
/// Namespace-only scanning is not enough here: unlike most ABP modules, this
/// codebase organizes Domain/Application.Contracts/Application by *feature
/// slice* rather than by project, so e.g. the ToolInstance entity (Domain) and
/// ToolInstanceLookupDto (Application.Contracts) legitimately share the
/// namespace <c>ToolShare.Catalog.ToolInstances</c>. The bare-identifier check
/// below is what actually distinguishes "referenced the entity" from
/// "referenced the DTO".
/// </summary>
public class ContractIsolationTests
{
    /// <summary>Namespaces that exist only in the EntityFrameworkCore project or are Domain-only infrastructure (never part of the published boundary).</summary>
    private static readonly string[] ForbiddenNamespacePrefixes =
    {
        "ToolShare.Catalog.EntityFrameworkCore",
        "ToolShare.Catalog.Repositories",
        "ToolShare.Catalog.Migrations",
        "ToolShare.Catalog.Data"
    };

    /// <summary>
    /// Concrete Domain/EntityFrameworkCore type names, matched as whole
    /// identifiers (word-boundary) so that e.g. "Tool" does not falsely match
    /// inside "ToolDto"/"IToolAppService"/"ToolId".
    /// </summary>
    private static readonly string[] ForbiddenBareIdentifiers =
    {
        "CatalogDbContext",
        "EfCoreCategoryRepository",
        "EfCoreToolRepository",
        "EfCoreToolInstanceRepository",
        "ICategoryRepository",
        "IToolRepository",
        "IToolInstanceRepository",
        "ICatalogDbContext",
        "CategoryManager",
        "ToolInstanceManager",
        "ToolInstanceLookupRow",
        "Category",
        "Tool",
        "ToolInstance",
        "ToolInstancePhoto",
        "ToolInstanceStateChange"
    };

    [Fact]
    public void PublicContract_test_files_do_not_import_Catalog_Domain_or_EntityFrameworkCore_namespaces()
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
    public void PublicContract_test_files_do_not_reference_Catalog_Domain_types_by_bare_name()
    {
        var violations = new List<string>();

        foreach (var file in GetPublicContractSourceFiles())
        {
            var text = StripCommentsAndStrings(File.ReadAllText(file));

            foreach (var identifier in ForbiddenBareIdentifiers)
            {
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(identifier)}\b"))
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
        // Order matters: block comments before line comments before strings,
        // each pass replaced with same-length whitespace to keep positions stable.
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

        var publicContractDir = Path.Combine(current.FullName, "test", "ToolShare.Catalog.Application.Tests", "PublicContract");
        if (!Directory.Exists(publicContractDir))
        {
            throw new InvalidOperationException("PublicContract test directory not found at " + publicContractDir);
        }

        return publicContractDir;
    }
}
