namespace Dobby.PluginFramework.Manifest;

// The manifest v2 contract has exactly one typed model, `PluginManifest`, which is what
// `PluginPackageReader` projects a validated manifest into. A parallel `PluginManifestV2`
// record used to live here; it duplicated that shape, was consumed by nothing, and left
// two public types competing to look authoritative. The enforced contract is the embedded
// `docs/schemas/dobby-plugin-manifest-v2.schema.json` document plus these constants.
public static class PluginManifestContract
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;
    public const string PackagePurpose = "dobby.plugin-package";
    public const string SignatureAlgorithm = "ECDSA-P256-SHA256";
    public const string SignatureFormat = "IeeeP1363FixedFieldConcatenation";
    public const int MaximumFiles = 256;
}
