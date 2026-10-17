namespace SGSClient.Core.DTOs;

/// <summary>Limits shared by the creative content API and its callers.</summary>
public static class CreativeRules
{
    public const int MaxTitle = 120;
    public const int MaxDescription = 4000;
    public const int MaxComment = 2000;
    public const int MaxTags = 10;
    public const int MaxTagLength = 30;
    public const int MaxChapters = 200;
    public const int MaxChapterTitle = 200;
    public const int MaxChapterText = 200_000;

    public const long MaxArtBytes = 25L * 1024 * 1024;
    public const long MaxMusicBytes = 100L * 1024 * 1024;
    public const long MaxCoverBytes = 10L * 1024 * 1024;

    public static readonly string[] ArtExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif"];
    public static readonly string[] MusicExtensions = [".mp3", ".flac", ".wav", ".ogg", ".m4a"];
    public static readonly string[] CoverExtensions = [".png", ".jpg", ".jpeg", ".webp"];
}
