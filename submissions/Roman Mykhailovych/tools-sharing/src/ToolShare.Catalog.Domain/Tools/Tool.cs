using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Catalog.Tools;

/// <summary>
/// The logical model of a tool (name, owning category); groups one or more
/// <see cref="ToolInstances.ToolInstance"/> aggregates. Instance uniqueness and
/// lifecycle are tracked separately on <see cref="ToolInstances.ToolInstance"/>.
/// </summary>
public class Tool : FullAuditedAggregateRoot<Guid>
{
    public virtual Guid CategoryId { get; private set; }

    public virtual string Name { get; private set; } = null!;

    /// <summary>
    /// Derived from <see cref="Name"/> via <see cref="CatalogTextNormalizer"/>.
    /// Indexed (not unique — two tools may share a name in different categories);
    /// backs the accent- and case-insensitive search (TR-03, FR-007, SC-008).
    /// </summary>
    public virtual string NormalizedName { get; private set; } = null!;

    public virtual string? Description { get; private set; }

    protected Tool()
    {
    }

    public Tool(Guid id, Guid categoryId, string name, string? description = null)
        : base(id)
    {
        CategoryId = categoryId;
        SetName(name);
        SetDescription(description);
    }

    /// <summary>Enforces TR-01/TR-03.</summary>
    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
        Check.Length(Name, nameof(name), CatalogDomainSharedConsts.ToolNameMaxLength, CatalogDomainSharedConsts.ToolNameMinLength);
        NormalizedName = CatalogTextNormalizer.Normalize(Name);
    }

    public void SetDescription(string? description)
    {
        description = description?.Trim();
        Check.Length(description, nameof(description), CatalogDomainSharedConsts.ToolDescriptionMaxLength);
        Description = description;
    }

    /// <summary>Enforces TR-02 (existence of the target category is checked by the caller with repository access).</summary>
    public void ChangeCategory(Guid categoryId)
    {
        CategoryId = categoryId;
    }
}
