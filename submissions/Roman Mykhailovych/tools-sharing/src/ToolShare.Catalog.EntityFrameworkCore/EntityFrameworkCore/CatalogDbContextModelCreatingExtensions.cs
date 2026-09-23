using Microsoft.EntityFrameworkCore;
using ToolShare.Catalog.Categories;
using ToolShare.Catalog.Tools;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ToolShare.Catalog.EntityFrameworkCore;

public static class CatalogDbContextModelCreatingExtensions
{
    public static void ConfigureCatalog(this ModelBuilder builder)
    {
        builder.Entity<Category>(b =>
        {
            b.ToTable(CatalogDbProperties.DbTablePrefix + "Categories", CatalogDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(CatalogDomainSharedConsts.CategoryNameMaxLength);
            b.Property(x => x.NormalizedName).IsRequired().HasMaxLength(CatalogDomainSharedConsts.CategoryNameMaxLength);
            b.Property(x => x.Description).HasMaxLength(CatalogDomainSharedConsts.CategoryDescriptionMaxLength);

            b.HasIndex(x => x.NormalizedName).IsUnique();
        });

        builder.Entity<Tool>(b =>
        {
            b.ToTable(CatalogDbProperties.DbTablePrefix + "Tools", CatalogDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(CatalogDomainSharedConsts.ToolNameMaxLength);
            b.Property(x => x.NormalizedName).IsRequired().HasMaxLength(CatalogDomainSharedConsts.ToolNameMaxLength);
            b.Property(x => x.Description).HasMaxLength(CatalogDomainSharedConsts.ToolDescriptionMaxLength);

            b.HasIndex(x => x.NormalizedName);
            b.HasIndex(x => x.CategoryId);

            b.HasOne<Category>()
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        builder.Entity<ToolInstance>(b =>
        {
            b.ToTable(CatalogDbProperties.DbTablePrefix + "ToolInstances", CatalogDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.SerialNumber).IsRequired().HasMaxLength(CatalogDomainSharedConsts.SerialNumberMaxLength);
            b.Property(x => x.NormalizedSerialNumber).IsRequired().HasMaxLength(CatalogDomainSharedConsts.SerialNumberMaxLength);
            b.Property(x => x.RetirementReason).HasMaxLength(CatalogDomainSharedConsts.RetirementReasonMaxLength);
            b.Property(x => x.Notes).HasMaxLength(CatalogDomainSharedConsts.NotesMaxLength);

            b.Ignore(x => x.IsAvailable);

            b.HasIndex(x => x.NormalizedSerialNumber).IsUnique();
            b.HasIndex(x => x.ToolId);
            b.HasIndex(x => new { x.CirculationState, x.Condition });

            b.HasOne<Tool>()
                .WithMany()
                .HasForeignKey(x => x.ToolId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.HasMany(x => x.Photos)
                .WithOne()
                .HasForeignKey(x => x.ToolInstanceId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasMany(x => x.StateHistory)
                .WithOne()
                .HasForeignKey(x => x.ToolInstanceId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Metadata.FindNavigation(nameof(ToolInstance.Photos))!.SetPropertyAccessMode(PropertyAccessMode.Field);
            b.Metadata.FindNavigation(nameof(ToolInstance.StateHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ToolInstancePhoto>(b =>
        {
            b.ToTable(CatalogDbProperties.DbTablePrefix + "ToolInstancePhotos", CatalogDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.BlobName).IsRequired().HasMaxLength(CatalogDomainSharedConsts.PhotoBlobNameMaxLength);
            b.Property(x => x.FileName).IsRequired().HasMaxLength(CatalogDomainSharedConsts.PhotoFileNameMaxLength);
            b.Property(x => x.ContentType).IsRequired().HasMaxLength(CatalogDomainSharedConsts.PhotoContentTypeMaxLength);

            b.HasIndex(x => x.ToolInstanceId);
        });

        builder.Entity<ToolInstanceStateChange>(b =>
        {
            b.ToTable(CatalogDbProperties.DbTablePrefix + "ToolInstanceStateChanges", CatalogDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Reason).HasMaxLength(CatalogDomainSharedConsts.ConditionChangeReasonMaxLength);

            b.HasIndex(x => new { x.ToolInstanceId, x.ChangedAt });
        });
    }
}
