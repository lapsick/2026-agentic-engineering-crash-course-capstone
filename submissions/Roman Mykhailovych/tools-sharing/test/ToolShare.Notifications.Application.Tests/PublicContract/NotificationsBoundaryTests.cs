using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ToolShare.Notifications;

/// <summary>
/// Constitution Principle II: Notifications' own production code
/// (<c>src/ToolShare.Notifications.Application</c>, <c>src/ToolShare.Notifications.Domain</c>)
/// must never reference Lending's, Membership's, or Catalog's Domain or
/// EntityFrameworkCore layer — only their published <c>Application.Contracts</c>
/// (and, for the two source events, their <c>Domain.Shared</c>). Mirrors
/// <c>ToolShare.Lending.LendingModuleBoundaryTests</c> (004), the reverse-
/// direction check Notifications needs for the same reason Lending did:
/// it is a heavy *consumer* of three other modules' published surfaces.
/// </summary>
public class NotificationsBoundaryTests
{
    private static readonly string[] ForbiddenNamespacePrefixes =
    {
        "ToolShare.Catalog.EntityFrameworkCore",
        "ToolShare.Catalog.Repositories",
        "ToolShare.Catalog.Migrations",
        "ToolShare.Membership.EntityFrameworkCore",
        "ToolShare.Membership.Repositories",
        "ToolShare.Membership.Migrations",
        "ToolShare.Lending.EntityFrameworkCore",
        "ToolShare.Lending.Repositories",
        "ToolShare.Lending.Migrations"
    };

    private static readonly string[] ForbiddenBareIdentifiers =
    {
        // Catalog Domain — aggregate roots/entities, repository interfaces, domain services
        "Tool",
        "Category",
        "ToolInstance",
        "ToolInstancePhoto",
        "ToolInstanceStateChange",
        "IToolRepository",
        "ICategoryRepository",
        "IToolInstanceRepository",
        "ToolInstanceManager",
        "CategoryManager",

        // Membership Domain — aggregate roots/entities, repository interfaces, domain services
        "Member",
        "CommunityRules",
        "MemberManager",
        "ReliabilityPolicy",
        "MemberStandingChange",
        "IMemberRepository",
        "ICommunityRulesRepository",

        // Lending Domain — aggregate roots/entities, repository interfaces, domain services
        "Reservation",
        "WaitlistEntry",
        "Loan",
        "MaintenanceRequest",
        "IReservationRepository",
        "IWaitlistEntryRepository",
        "ILoanRepository",
        "IMaintenanceRequestRepository",
        "ReservationManager",
        "WaitlistManager",
        "LoanManager"
    };

    [Fact]
    public void Notifications_production_code_does_not_import_other_modules_EntityFrameworkCore_namespaces()
    {
        var violations = new List<string>();

        foreach (var file in GetNotificationsProductionSourceFiles())
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
    public void Notifications_production_code_does_not_reference_other_modules_Domain_types_by_bare_name()
    {
        var violations = new List<string>();

        foreach (var file in GetNotificationsProductionSourceFiles())
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

    private static string StripCommentsAndStrings(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", m => new string(' ', m.Length), RegexOptions.Singleline);
        source = Regex.Replace(source, @"//.*$", m => new string(' ', m.Length), RegexOptions.Multiline);
        source = Regex.Replace(source, "\"(?:[^\"\\\\]|\\\\.)*\"", m => new string(' ', m.Length));
        return source;
    }

    private static IEnumerable<string> GetNotificationsProductionSourceFiles()
    {
        var root = FindRepositoryRoot();

        var directories = new[]
        {
            Path.Combine(root, "src", "ToolShare.Notifications.Application"),
            Path.Combine(root, "src", "ToolShare.Notifications.Domain")
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
