namespace ToolShare.Catalog;

public static class CatalogDomainSharedConsts
{
    public const int CategoryNameMinLength = 2;
    public const int CategoryNameMaxLength = 128;
    public const int CategoryDescriptionMaxLength = 512;

    public const int ToolNameMinLength = 2;
    public const int ToolNameMaxLength = 256;
    public const int ToolDescriptionMaxLength = 2048;

    public const int SerialNumberMinLength = 1;
    public const int SerialNumberMaxLength = 64;

    public const int RetirementReasonMaxLength = 512;
    public const int ConditionChangeReasonMaxLength = 512;
    public const int NotesMaxLength = 1024;

    public const int PhotoFileNameMaxLength = 256;
    public const int PhotoBlobNameMaxLength = 256;
    public const int PhotoContentTypeMaxLength = 128;
}
