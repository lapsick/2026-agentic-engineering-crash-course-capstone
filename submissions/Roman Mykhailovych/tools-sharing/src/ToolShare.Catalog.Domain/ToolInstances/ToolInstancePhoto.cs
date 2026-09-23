using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace ToolShare.Catalog.ToolInstances;

/// <summary>
/// Metadata for a photo attached to a <see cref="ToolInstance"/>. The binary
/// content lives in ABP BlobStoring (FileSystem provider), never in the database
/// (PR-03) — only <see cref="BlobName"/> is persisted here.
/// </summary>
public class ToolInstancePhoto : Entity<Guid>
{
    public virtual Guid ToolInstanceId { get; private set; }

    /// <summary>Key in the <c>ToolInstancePhotoContainer</c> blob container.</summary>
    public virtual string BlobName { get; private set; } = null!;

    /// <summary>Original upload name, shown to users.</summary>
    public virtual string FileName { get; private set; } = null!;

    public virtual string ContentType { get; private set; } = null!;

    public virtual long SizeBytes { get; private set; }

    public virtual int DisplayOrder { get; internal set; }

    public virtual bool IsPrimary { get; internal set; }

    protected ToolInstancePhoto()
    {
    }

    internal ToolInstancePhoto(
        Guid id,
        Guid toolInstanceId,
        string blobName,
        string fileName,
        string contentType,
        long sizeBytes,
        int displayOrder,
        bool isPrimary)
        : base(id)
    {
        ToolInstanceId = toolInstanceId;
        BlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName));
        Check.Length(BlobName, nameof(blobName), CatalogDomainSharedConsts.PhotoBlobNameMaxLength);
        FileName = Check.NotNullOrWhiteSpace(fileName, nameof(fileName));
        Check.Length(FileName, nameof(fileName), CatalogDomainSharedConsts.PhotoFileNameMaxLength);
        ContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType));
        Check.Length(ContentType, nameof(contentType), CatalogDomainSharedConsts.PhotoContentTypeMaxLength);
        SizeBytes = sizeBytes;
        DisplayOrder = displayOrder;
        IsPrimary = isPrimary;
    }
}
