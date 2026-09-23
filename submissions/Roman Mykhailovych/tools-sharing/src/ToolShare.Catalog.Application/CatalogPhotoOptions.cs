using System.Collections.Generic;

namespace ToolShare.Catalog;

public class CatalogPhotoOptions
{
    public long MaxSizeBytes { get; set; } = 5 * 1024 * 1024;

    public List<string> AllowedContentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public int MaxPerInstance { get; set; } = 5;
}
