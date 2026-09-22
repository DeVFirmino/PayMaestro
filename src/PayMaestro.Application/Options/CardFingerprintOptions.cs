namespace PayMaestro.Application.Options;

public sealed class CardFingerprintOptions
{
    public const string SectionName = "CardFingerprint";

    /// <summary>
    /// Every development key starts with this. The validator refuses such a key outside
    /// Development, so the one committed to appsettings.Development.json can never sign real cards.
    /// </summary>
    public const string DevelopmentKeyPrefix = "development-only-";

    public const int MinimumKeyLength = 32;

    public string Key { get; set; } = string.Empty;
}
