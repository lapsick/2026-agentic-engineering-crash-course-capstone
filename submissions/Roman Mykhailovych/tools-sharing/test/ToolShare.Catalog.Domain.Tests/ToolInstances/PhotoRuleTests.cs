using System;
using System.Linq;
using Shouldly;
using ToolShare.Catalog.ToolInstances;
using Volo.Abp;
using Xunit;

namespace ToolShare.Catalog.ToolInstances;

public class PhotoRuleTests
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxSizeBytes = 5 * 1024 * 1024;
    private const int MaxPhotos = 5;

    private static ToolInstance CreateInstance()
    {
        return new ToolInstance(Guid.NewGuid(), Guid.NewGuid(), "RH-001", ToolCondition.Good, DateTime.UtcNow, null);
    }

    [Fact]
    public void First_photo_added_becomes_primary()
    {
        var instance = CreateInstance();

        var photo = instance.AddPhoto("blob-1", "front.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);

        photo.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void Subsequent_photos_are_not_primary_by_default()
    {
        var instance = CreateInstance();
        instance.AddPhoto("blob-1", "front.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);

        var second = instance.AddPhoto("blob-2", "back.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);

        second.IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void Adding_a_photo_beyond_the_max_is_rejected()
    {
        var instance = CreateInstance();
        for (var i = 0; i < MaxPhotos; i++)
        {
            instance.AddPhoto($"blob-{i}", $"photo-{i}.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);
        }

        var exception = Should.Throw<BusinessException>(() =>
            instance.AddPhoto("blob-overflow", "overflow.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos));

        exception.Code.ShouldBe("Catalog:TooManyPhotos");
        instance.Photos.Count.ShouldBe(MaxPhotos);
    }

    [Fact]
    public void Unsupported_content_type_is_rejected()
    {
        var instance = CreateInstance();

        var exception = Should.Throw<BusinessException>(() =>
            instance.AddPhoto("blob-1", "doc.txt", "text/plain", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos));

        exception.Code.ShouldBe("Catalog:UnsupportedPhotoFormat");
    }

    [Fact]
    public void Oversized_photo_is_rejected()
    {
        var instance = CreateInstance();

        var exception = Should.Throw<BusinessException>(() =>
            instance.AddPhoto("blob-1", "huge.jpg", "image/jpeg", MaxSizeBytes + 1, AllowedContentTypes, MaxSizeBytes, MaxPhotos));

        exception.Code.ShouldBe("Catalog:PhotoTooLarge");
    }

    [Fact]
    public void Removing_the_primary_photo_promotes_the_next_by_display_order()
    {
        var instance = CreateInstance();
        var first = instance.AddPhoto("blob-1", "front.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);
        var second = instance.AddPhoto("blob-2", "back.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);

        instance.RemovePhoto(first.Id);

        instance.Photos.Count.ShouldBe(1);
        instance.Photos.Single().Id.ShouldBe(second.Id);
        instance.Photos.Single().IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void SetPrimaryPhoto_moves_the_primary_flag()
    {
        var instance = CreateInstance();
        var first = instance.AddPhoto("blob-1", "front.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);
        var second = instance.AddPhoto("blob-2", "back.jpg", "image/jpeg", 1024, AllowedContentTypes, MaxSizeBytes, MaxPhotos);

        instance.SetPrimaryPhoto(second.Id);

        first.IsPrimary.ShouldBeFalse();
        second.IsPrimary.ShouldBeTrue();
    }
}
