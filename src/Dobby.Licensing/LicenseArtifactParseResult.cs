namespace Dobby.Licensing;

public sealed record LicenseArtifactParseResult(
    bool IsSuccess,
    LicenseArtifact? Artifact,
    string ReasonCode)
{
    public static LicenseArtifactParseResult Success(LicenseArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new LicenseArtifactParseResult(true, artifact, string.Empty);
    }

    public static LicenseArtifactParseResult Failure(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        return new LicenseArtifactParseResult(false, null, reasonCode);
    }
}
