using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace ToolShare.Catalog.Categories;

/// <summary>
/// A classification for tools. A tool belongs to exactly one category;
/// a category cannot be removed while tools reference it (CR-04, enforced
/// by <see cref="CategoryManager"/>, not here — that check needs a repository).
/// </summary>
public class Category : FullAuditedAggregateRoot<Guid>
{
    public virtual string Name { get; private set; } = null!;

    /// <summary>
    /// Derived from <see cref="Name"/> via <see cref="CatalogTextNormalizer"/>.
    /// Never assigned directly (CR-02). Carries the catalog-wide unique index (CR-03).
    /// </summary>
    public virtual string NormalizedName { get; private set; } = null!;

    public virtual string? Description { get; private set; }

    protected Category()
    {
    }

    public Category(Guid id, string name, string? description = null)
        : base(id)
    {
        SetName(name);
        SetDescription(description);
    }

    /// <summary>Enforces CR-01/CR-02. Uniqueness (CR-03) is checked by the caller (<see cref="CategoryManager"/>).</summary>
    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name)).Trim();
        Check.Length(Name, nameof(name), CatalogDomainSharedConsts.CategoryNameMaxLength, CatalogDomainSharedConsts.CategoryNameMinLength);
        NormalizedName = CatalogTextNormalizer.Normalize(Name);
    }

    public void SetDescription(string? description)
    {
        description = description?.Trim();
        Check.Length(description, nameof(description), CatalogDomainSharedConsts.CategoryDescriptionMaxLength);
        Description = description;
    }
}
