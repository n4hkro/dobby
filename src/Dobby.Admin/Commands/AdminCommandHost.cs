using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dobby.Licensing;
using Dobby.PluginFramework.Manifest;
using Dobby.PluginFramework.Packaging;
using Dobby.Security.Authentication;
using Dobby.Security.Trust;

namespace Dobby.Admin.Commands;

public static class AdminCommandHost
{
    private const string SignPluginPackage = "sign-plugin-package";
    private const string CreatePluginLicense = "create-plugin-license";
    private const string CreateRecoveryResponse = "create-recovery-response";

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, CommandSpec> CommandSpecs = new Dictionary<string, CommandSpec>(StringComparer.Ordinal)
    {
        [SignPluginPackage] = new(
            [
                "--input-path",
                "--signing-profile",
                "--output-path"
            ],
            [
                "--trust-profile",
                "--report-path",
                "--overwrite"
            ]),
        [CreatePluginLicense] = new(
            [
                "--plugin-package-path",
                "--signing-profile",
                "--customer-id",
                "--deployment-id",
                "--device-binding-fixture-path",
                "--valid-from",
                "--valid-until",
                "--revision",
                "--output-path"
            ],
            [
                "--report-path",
                "--intent",
                "--overwrite"
            ]),
        [CreateRecoveryResponse] = new(
            [
                "--request-path",
                "--device-public-key-path",
                "--signing-profile",
                "--output-path"
            ],
            [
                "--report-path",
                "--overwrite"
            ])
    };

    public static AdminCommandResult Run(IReadOnlyList<string> args)
    {
        return Run(args, new EnvironmentAdminSigningMaterialProvider(), TimeProvider.System);
    }

    public static AdminCommandResult Run(IReadOnlyList<string> args, TimeProvider? timeProvider)
    {
        return Run(args, new EnvironmentAdminSigningMaterialProvider(), timeProvider ?? TimeProvider.System);
    }

    public static AdminCommandResult Run(
        IReadOnlyList<string> args,
        IAdminSigningMaterialProvider signingMaterialProvider,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(signingMaterialProvider);

        try
        {
            if (args.Count < 2 || !args[0].Equals("--cli", StringComparison.Ordinal))
            {
                return Usage("usage-error");
            }

            var command = args[1];
            if (!CommandSpecs.TryGetValue(command, out var spec))
            {
                return Usage("unknown-command");
            }

            var parsed = ParseOptions(args, spec);
            if (!parsed.IsSuccess)
            {
                return Usage(parsed.Code);
            }

            var options = parsed.Options!;
            var outputPath = Require(options, "--output-path");
            var reportPath = options.Values.TryGetValue("--report-path", out var explicitReportPath)
                ? explicitReportPath
                : outputPath + ".report.json";
            var overwrite = options.Switches.Contains("--overwrite");

            if (!TryParseProfile(Require(options, "--signing-profile"), out var signingProfile))
            {
                return Validation("unknown-profile");
            }

            if (command == SignPluginPackage)
            {
                var trustProfile = options.Values.TryGetValue("--trust-profile", out var explicitTrustProfile)
                    ? explicitTrustProfile
                    : signingProfile;

                if (!TryParseProfile(trustProfile, out var mappedTrustProfile))
                {
                    return Validation("unknown-profile");
                }

                if (!mappedTrustProfile.Equals(signingProfile, StringComparison.Ordinal))
                {
                    return Validation("profile-mismatch");
                }
            }

            if (command == CreatePluginLicense)
            {
                if (!TryParseUtcZ(Require(options, "--valid-from"), out var validFrom) ||
                    !TryParseUtcZ(Require(options, "--valid-until"), out var validUntil) ||
                    validUntil <= validFrom)
                {
                    return Validation("invalid-timestamp");
                }

                if (!int.TryParse(Require(options, "--revision"), NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision <= 0)
                {
                    return Validation("invalid-revision");
                }

                if (options.Values.TryGetValue("--intent", out var intent) &&
                    intent is not "granted" and not "revoked")
                {
                    return Validation("invalid-intent");
                }
            }

            if (File.Exists(outputPath) && !overwrite)
            {
                return Validation("output-conflict");
            }

            if (File.Exists(reportPath) && !overwrite)
            {
                return Validation("report-conflict");
            }

            var signingPurpose = command switch
            {
                SignPluginPackage => ArtifactSignaturePurposes.PluginPackage,
                CreatePluginLicense => ArtifactSignaturePurposes.PluginLicense,
                CreateRecoveryResponse => ArtifactSignaturePurposes.OwnerRecoveryResponse,
                _ => throw new InvalidOperationException("Unknown signing purpose.")
            };
            var material = signingMaterialProvider.Resolve(signingProfile, signingPurpose);
            if (!material.IsSuccess)
            {
                return new AdminCommandResult(66, string.Empty, "signing-material-unavailable");
            }

            return command switch
            {
                SignPluginPackage => SignPluginPackageCommand(options, outputPath, reportPath, material.Value!, overwrite),
                CreatePluginLicense => CreatePluginLicenseCommand(options, outputPath, reportPath, material.Value!, overwrite),
                CreateRecoveryResponse => CreateRecoveryResponseCommand(options, outputPath, reportPath, material.Value!, overwrite, timeProvider ?? TimeProvider.System),
                _ => Usage("unknown-command")
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or CryptographicException)
        {
            return new AdminCommandResult(70, string.Empty, "internal-failure");
        }
    }

    private static AdminCommandResult SignPluginPackageCommand(
        ParsedOptions options,
        string outputPath,
        string reportPath,
        AdminSigningMaterial material,
        bool overwrite)
    {
        var inputPath = Require(options, "--input-path");
        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            return Validation("input-not-found");
        }

        if (!material.Purpose.Equals(ArtifactSignaturePurposes.PluginPackage, StringComparison.Ordinal) ||
            Path.GetFullPath(inputPath).Equals(Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            return Validation("invalid-output-path");
        }

        var read = PluginPackageReader.Read(inputPath);
        if (!read.IsSuccess)
        {
            return Validation("invalid-package");
        }

        var archive = read.Value;
        var packageEntries = new Dictionary<string, PluginPackageEntry>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.Where(entry => !entry.Path.Equals(archive.ManifestPath, StringComparison.OrdinalIgnoreCase)))
        {
            if (!packageEntries.TryAdd(entry.Path, entry))
            {
                return Validation("invalid-package");
            }
        }

        var refreshedFiles = new List<PluginPackageFile>();
        foreach (var file in archive.Manifest.Files)
        {
            if (!packageEntries.TryGetValue(file.Path, out var entry))
            {
                return Validation("invalid-package");
            }

            refreshedFiles.Add(file with { Sha256 = entry.Sha256 });
        }

        var refreshedManifest = archive.Manifest with { Files = refreshedFiles };
        var fingerprint = PluginPackageFingerprint.Compute(refreshedManifest);
        var signature = ArtifactSignatureCodec.ToUnpaddedBase64Url(material.Key.SignData(
            fingerprint.CanonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var signedManifest = refreshedManifest with
        {
            Signature = new PluginPackageSignature(
                ArtifactSignaturePurposes.PluginPackage,
                ArtifactSignatureProfile.Algorithm,
                ArtifactSignatureProfile.Format,
                material.KeyId,
                signature)
        };
        var manifestJson = WritePluginManifestJson(signedManifest);

        EnsureParentDirectory(outputPath);
        using (var stream = File.Create(outputPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            AddZipEntry(zip, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
            foreach (var file in signedManifest.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
            {
                var entry = packageEntries[file.Path];
                using var content = entry.OpenRead();
                AddZipEntry(zip, entry.Path, content);
            }
        }

        var report = new
        {
            command = SignPluginPackage,
            signingProfile = material.Profile,
            signingPurpose = material.Purpose,
            signingKeyId = material.KeyId,
            outputFileName = Path.GetFileName(outputPath),
            reportFileName = Path.GetFileName(reportPath),
            packageFingerprint = fingerprint.Fingerprint,
            artifactSha256 = Sha256File(outputPath)
        };
        WriteReport(reportPath, report, overwrite);

        return Success(SignPluginPackage, outputPath, reportPath);
    }

    private static AdminCommandResult CreatePluginLicenseCommand(
        ParsedOptions options,
        string outputPath,
        string reportPath,
        AdminSigningMaterial material,
        bool overwrite)
    {
        var packagePath = Require(options, "--plugin-package-path");
        var bindingPath = Require(options, "--device-binding-fixture-path");
        if (!File.Exists(packagePath) || !File.Exists(bindingPath))
        {
            return Validation("input-not-found");
        }

        if (!TryParseUtcZ(Require(options, "--valid-from"), out var validFrom) ||
            !TryParseUtcZ(Require(options, "--valid-until"), out var validUntil) ||
            !int.TryParse(Require(options, "--revision"), NumberStyles.None, CultureInfo.InvariantCulture, out var revision))
        {
            return Validation("invalid-input");
        }

        var deviceFingerprint = ReadDeviceFingerprint(bindingPath);
        if (deviceFingerprint is null)
        {
            return Validation("invalid-device-binding");
        }

        var pluginId = ReadPluginId(packagePath) ?? Path.GetFileNameWithoutExtension(packagePath);
        var packageFingerprint = ReadPackageFingerprint(packagePath);
        var customerId = Require(options, "--customer-id");
        var deploymentId = Require(options, "--deployment-id");
        var intent = options.Values.TryGetValue("--intent", out var explicitIntent)
            ? explicitIntent
            : "granted";
        var revisionSeriesId = "series-" + Sha256Text($"{pluginId}\0{customerId}\0{deploymentId}")[..32];
        var payload = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customerId"] = customerId,
            ["deploymentId"] = deploymentId,
            ["deviceKeyHash"] = deviceFingerprint,
            ["expiresAt"] = ToUtcZ(validUntil),
            ["gracePeriodDays"] = 14,
            ["intent"] = intent,
            ["issuedAt"] = ToUtcZ(validFrom),
            ["keyId"] = material.KeyId,
            ["licenseId"] = Guid.NewGuid().ToString("D"),
            ["licenseRevision"] = revision,
            ["notBefore"] = ToUtcZ(validFrom),
            ["packageFingerprint"] = packageFingerprint,
            ["pluginId"] = pluginId,
            ["purpose"] = ArtifactSignaturePurposes.PluginLicense,
            ["revisionSeriesId"] = revisionSeriesId,
            ["schemaVersion"] = LicenseV3Contract.SchemaVersion
        };

        using var payloadDocument = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var canonicalJson = LicenseCanonicalJsonWriter.Write(payloadDocument.RootElement);
        var canonicalPayload = ArtifactCanonicalPayload.Compose(
            ArtifactSignaturePurposes.PluginLicense,
            LicenseV3Contract.SchemaVersion,
            canonicalJson);
        var signature = ArtifactSignatureCodec.ToUnpaddedBase64Url(material.Key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var payloadJson = Encoding.UTF8.GetString(canonicalJson);
        var artifactJson = "{\"payload\":" + payloadJson +
            ",\"signature\":{\"purpose\":\"dobby.plugin-license\",\"algorithm\":\"ECDSA-P256-SHA256\",\"format\":\"IeeeP1363FixedFieldConcatenation\",\"keyId\":\"" +
            material.KeyId +
            "\",\"value\":\"" +
            signature +
            "\"}}";

        EnsureParentDirectory(outputPath);
        File.WriteAllText(outputPath, artifactJson, new UTF8Encoding(false));

        var report = new
        {
            command = CreatePluginLicense,
            signingProfile = material.Profile,
            signingPurpose = material.Purpose,
            signingKeyId = material.KeyId,
            outputFileName = Path.GetFileName(outputPath),
            reportFileName = Path.GetFileName(reportPath),
            pluginId,
            customerIdHash = Sha256Text(Require(options, "--customer-id"))[..12],
            deploymentIdHash = Sha256Text(Require(options, "--deployment-id"))[..12],
            licenseRevision = revision,
            licenseIntent = intent,
            artifactSha256 = Sha256File(outputPath)
        };
        WriteReport(reportPath, report, overwrite);

        return Success(CreatePluginLicense, outputPath, reportPath);
    }

    private static AdminCommandResult CreateRecoveryResponseCommand(
        ParsedOptions options,
        string outputPath,
        string reportPath,
        AdminSigningMaterial material,
        bool overwrite,
        TimeProvider timeProvider)
    {
        var requestPath = Require(options, "--request-path");
        if (!File.Exists(requestPath))
        {
            return Validation("input-not-found");
        }

        var devicePublicKeyPath = Require(options, "--device-public-key-path");
        if (!File.Exists(devicePublicKeyPath))
        {
            return Validation("input-not-found");
        }

        RecoveryRequestEnvelope requestEnvelope;
        byte[] devicePublicKey;
        try
        {
            requestEnvelope = RecoveryRequestVerifier.ReadEnvelope(File.ReadAllText(requestPath));
            devicePublicKey = RecoveryRequestVerifier.ReadPublicKey(devicePublicKeyPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            return Validation("invalid-recovery-request");
        }

        var requestVerification = new RecoveryRequestVerifier().Verify(
            requestEnvelope,
            devicePublicKey,
            timeProvider.GetUtcNow());
        if (!requestVerification.IsSuccess ||
            !StringComparer.Ordinal.Equals(material.Purpose, ArtifactSignaturePurposes.OwnerRecoveryResponse) ||
            !StringComparer.Ordinal.Equals(material.KeyId, requestEnvelope.Payload.VendorKeyId))
        {
            return Validation("invalid-recovery-request");
        }

        var request = requestVerification.Value;
        var issuedAt = timeProvider.GetUtcNow().ToUniversalTime();
        var payload = new RecoveryResponsePayload(
            2,
            ArtifactSignaturePurposes.OwnerRecoveryResponse,
            material.KeyId,
            Guid.CreateVersion7().ToString("D"),
            request.RequestId,
            request.Nonce,
            request.CustomerId,
            request.DeploymentId,
            request.DeviceId,
            request.RecoveryPublicKeyHash,
            "reset-owner-password",
            ToUtcZ(issuedAt),
            ToUtcZ(issuedAt.AddHours(24)));
        var canonicalPayload = RecoveryCanonicalJsonWriter.WritePayload(payload);
        var signatureValue = ArtifactSignatureCodec.ToUnpaddedBase64Url(material.Key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var envelope = new RecoveryResponseEnvelope(
            payload,
            new ArtifactSignatureEnvelope(
                ArtifactSignaturePurposes.OwnerRecoveryResponse,
                ArtifactSignatureProfile.Algorithm,
                ArtifactSignatureProfile.Format,
                material.KeyId,
                signatureValue));

        EnsureParentDirectory(outputPath);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(envelope, IndentedJsonOptions), new UTF8Encoding(false));

        var report = new
        {
            command = CreateRecoveryResponse,
            signingProfile = material.Profile,
            outputFileName = Path.GetFileName(outputPath),
            reportFileName = Path.GetFileName(reportPath),
            requestId = request.RequestId,
            issuedAt = ToUtcZ(issuedAt),
            expiresAt = ToUtcZ(issuedAt.AddHours(24)),
            artifactSha256 = Sha256File(outputPath)
        };
        WriteReport(reportPath, report, overwrite);

        return Success(CreateRecoveryResponse, outputPath, reportPath);
    }

    private static ParseResult ParseOptions(IReadOnlyList<string> args, CommandSpec spec)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        var allowed = spec.Required.Concat(spec.Optional).ToHashSet(StringComparer.Ordinal);

        for (var index = 2; index < args.Count; index++)
        {
            var option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || !allowed.Contains(option))
            {
                return ParseResult.Failure("invalid-option");
            }

            if (option.Equals("--overwrite", StringComparison.Ordinal))
            {
                if (!switches.Add(option))
                {
                    return ParseResult.Failure("repeated-option");
                }

                continue;
            }

            if (values.ContainsKey(option))
            {
                return ParseResult.Failure("repeated-option");
            }

            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return ParseResult.Failure("missing-option-value");
            }

            values[option] = args[++index];
        }

        if (!spec.Required.All(values.ContainsKey))
        {
            return ParseResult.Failure("missing-required-option");
        }

        return ParseResult.Success(new ParsedOptions(values, switches));
    }

    private static string? ReadDeviceFingerprint(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("deviceFingerprint", out var direct) &&
            direct.ValueKind == JsonValueKind.String &&
            IsLowercaseSha256(direct.GetString() ?? string.Empty))
        {
            return direct.GetString();
        }

        if (!root.TryGetProperty("machineGuid", out var machineGuidElement) ||
            machineGuidElement.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("stableDeviceIdHex", out var stableDeviceIdElement) ||
            stableDeviceIdElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var stableDeviceIdHex = stableDeviceIdElement.GetString() ?? string.Empty;
        if (stableDeviceIdHex.Length == 0 || stableDeviceIdHex.Length % 2 != 0 || stableDeviceIdHex.Any(character => !Uri.IsHexDigit(character)))
        {
            return null;
        }

        var computed = LicenseDeviceFingerprintV1.Compute(
            machineGuidElement.GetString() ?? string.Empty,
            Convert.FromHexString(stableDeviceIdHex));

        return computed.IsSuccess ? computed.Value.Value : null;
    }

    private static string? ReadPluginId(string packagePath)
    {
        var archive = PluginPackageReader.Read(packagePath);
        return archive.IsSuccess ? archive.Value.Manifest.PluginId : null;
    }

    private static string ReadPackageFingerprint(string packagePath)
    {
        var archive = PluginPackageReader.Read(packagePath);
        if (archive.IsSuccess)
        {
            return PluginPackageFingerprint.Compute(archive.Value.Manifest).Fingerprint;
        }

        return Sha256File(packagePath);
    }

    private static string WritePluginManifestJson(PluginManifest manifest)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
            writer.WriteString("purpose", manifest.Purpose);
            writer.WriteString("pluginId", manifest.PluginId);
            writer.WriteString("version", manifest.Version);
            writer.WriteString("vendorId", manifest.VendorId);
            writer.WriteString("hostApiVersion", manifest.HostApiVersion);
            writer.WriteString("minHostVersion", manifest.MinHostVersion);
            writer.WriteString("entryAssembly", manifest.EntryAssembly);
            writer.WriteString("displayName", manifest.DisplayName);
            writer.WriteString("description", manifest.Description);
            if (manifest.Icon is not null)
            {
                writer.WritePropertyName("icon");
                writer.WriteStartObject();
                writer.WriteString("path", manifest.Icon.Path);
                writer.WriteEndObject();
            }

            if (manifest.NativeLibraries.Count > 0)
            {
                writer.WritePropertyName("nativeLibraries");
                writer.WriteStartArray();
                foreach (var path in manifest.NativeLibraries)
                {
                    writer.WriteStringValue(path);
                }

                writer.WriteEndArray();
            }

            writer.WritePropertyName("dependsOn");
            writer.WriteStartArray();
            foreach (var dependency in manifest.DependsOn)
            {
                writer.WriteStartObject();
                writer.WriteString("pluginId", dependency.PluginId);
                writer.WriteString("minVersion", dependency.MinVersion);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("capabilities");
            writer.WriteStartArray();
            foreach (var capability in manifest.Capabilities)
            {
                writer.WriteStartObject();
                writer.WriteString("name", capability.Name);
                writer.WriteString("scope", capability.Scope);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("operationTargets");
            writer.WriteStartArray();
            foreach (var target in manifest.OperationTargets)
            {
                writer.WriteStartObject();
                writer.WriteString("id", target.Id);
                writer.WritePropertyName("verbs");
                writer.WriteStartArray();
                foreach (var verb in target.Verbs)
                {
                    writer.WriteStringValue(verb);
                }

                writer.WriteEndArray();
                writer.WriteString("labelKey", target.LabelKey);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("extensionPoints");
            writer.WriteStartArray();
            foreach (var extension in manifest.ExtensionPoints)
            {
                writer.WriteStartObject();
                writer.WriteString("id", extension.Id);
                writer.WriteString("target", extension.Target);
                writer.WriteString("routeKey", extension.RouteKey);
                if (extension.LabelKey is not null)
                {
                    writer.WriteString("labelKey", extension.LabelKey);
                }

                if (extension.ParentId is not null)
                {
                    writer.WriteString("parentId", extension.ParentId);
                }

                if (extension.Group is not null)
                {
                    writer.WriteString("group", extension.Group);
                }

                if (extension.Weight is not null)
                {
                    writer.WriteNumber("weight", extension.Weight.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("files");
            writer.WriteStartArray();
            foreach (var file in manifest.Files)
            {
                writer.WriteStartObject();
                writer.WriteString("path", file.Path);
                writer.WriteString("sha256", file.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("signature");
            writer.WriteStartObject();
            writer.WriteString("purpose", manifest.Signature.Purpose);
            writer.WriteString("algorithm", manifest.Signature.Algorithm);
            writer.WriteString("format", manifest.Signature.Format);
            writer.WriteString("keyId", manifest.Signature.KeyId);
            writer.WriteString("value", manifest.Signature.Value);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void AddZipEntry(ZipArchive zip, string path, byte[] content)
    {
        using var source = new MemoryStream(content, writable: false);
        AddZipEntry(zip, path, source);
    }

    private static void AddZipEntry(ZipArchive zip, string path, Stream content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        content.CopyTo(entryStream);
    }

    private static void WriteReport(string reportPath, object report, bool overwrite)
    {
        EnsureParentDirectory(reportPath);
        using var stream = new FileStream(reportPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, report, IndentedJsonOptions);
    }

    private static AdminCommandResult Success(string command, string outputPath, string reportPath)
    {
        var summary = $"{command}: success output={Path.GetFileName(outputPath)} report={Path.GetFileName(reportPath)}";
        return new AdminCommandResult(0, summary, string.Empty);
    }

    private static AdminCommandResult Usage(string code)
    {
        return new AdminCommandResult(64, string.Empty, $"usage-error: option contract is not approved ({code})");
    }

    private static AdminCommandResult Validation(string code)
    {
        return new AdminCommandResult(65, string.Empty, code);
    }

    private static string Require(ParsedOptions options, string name)
    {
        return options.Values[name];
    }

    private static bool TryParseProfile(string value, out string profile)
    {
        profile = value.ToLowerInvariant() switch
        {
            "dev-qa" => "dev-qa",
            "production" => "production",
            _ => string.Empty
        };

        return profile.Length > 0;
    }

    private static bool TryParseUtcZ(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;
        if (!value.EndsWith("Z", StringComparison.Ordinal))
        {
            return false;
        }

        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
        };
        return DateTimeOffset.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    private static string ToUtcZ(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sha256Text(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsLowercaseSha256(string value)
    {
        return value.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed record CommandSpec(
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Optional);

    private sealed record ParsedOptions(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Switches);

    private sealed record ParseResult(
        bool IsSuccess,
        ParsedOptions? Options,
        string Code)
    {
        public static ParseResult Success(ParsedOptions options)
        {
            return new ParseResult(true, options, string.Empty);
        }

        public static ParseResult Failure(string code)
        {
            return new ParseResult(false, null, code);
        }
    }
}

public interface IAdminSigningMaterialProvider
{
    AdminSigningMaterialResult Resolve(string signingProfile, string purpose);
}

public sealed record AdminSigningMaterial(
    string Profile,
    string Purpose,
    string KeyId,
    ECDsa Key);

public sealed record AdminSigningMaterialResult(
    bool IsSuccess,
    AdminSigningMaterial? Value,
    string Code)
{
    public static AdminSigningMaterialResult Success(AdminSigningMaterial value)
    {
        return new AdminSigningMaterialResult(true, value, string.Empty);
    }

    public static AdminSigningMaterialResult Unavailable(string code)
    {
        return new AdminSigningMaterialResult(false, null, code);
    }
}

public sealed class UnavailableAdminSigningMaterialProvider : IAdminSigningMaterialProvider
{
    public AdminSigningMaterialResult Resolve(string signingProfile, string purpose)
    {
        return AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
    }
}

public sealed class EnvironmentAdminSigningMaterialProvider : IAdminSigningMaterialProvider
{
    public AdminSigningMaterialResult Resolve(string signingProfile, string purpose)
    {
        var descriptor = (signingProfile, purpose) switch
        {
            ("dev-qa", ArtifactSignaturePurposes.PluginPackage) =>
                ("DOBBY_ADMIN_SIGNING_DEV_QA_PLUGIN_PACKAGE_KEY_PATH", "plugin-test-2026-01"),
            ("dev-qa", ArtifactSignaturePurposes.PluginLicense) =>
                ("DOBBY_ADMIN_SIGNING_DEV_QA_PLUGIN_LICENSE_KEY_PATH", "license-test-2026-01"),
            ("dev-qa", ArtifactSignaturePurposes.OwnerRecoveryResponse) =>
                ("DOBBY_ADMIN_SIGNING_DEV_QA_OWNER_RECOVERY_RESPONSE_KEY_PATH", VendorRecoveryPublicKey.DevQaKeyId),
            ("production", ArtifactSignaturePurposes.PluginPackage) =>
                ("DOBBY_ADMIN_SIGNING_PRODUCTION_PLUGIN_PACKAGE_KEY_PATH", "plugin-prod-2026-01"),
            ("production", ArtifactSignaturePurposes.PluginLicense) =>
                ("DOBBY_ADMIN_SIGNING_PRODUCTION_PLUGIN_LICENSE_KEY_PATH", "license-prod-2026-01"),
            ("production", ArtifactSignaturePurposes.OwnerRecoveryResponse) =>
                ("DOBBY_ADMIN_SIGNING_PRODUCTION_OWNER_RECOVERY_RESPONSE_KEY_PATH", VendorRecoveryPublicKey.ProductionKeyId),
            _ => (string.Empty, string.Empty)
        };
        if (string.IsNullOrWhiteSpace(descriptor.Item1))
        {
            return AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
        }

        var path = Environment.GetEnvironmentVariable(descriptor.Item1);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
        }

        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            key.ImportFromPem(File.ReadAllText(path));
            if (key.KeySize != 256)
            {
                throw new CryptographicException("Signing key must use P-256.");
            }

            return AdminSigningMaterialResult.Success(new AdminSigningMaterial(
                signingProfile,
                purpose,
                descriptor.Item2,
                key));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or CryptographicException)
        {
            key.Dispose();
            return AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
        }
    }
}

public sealed record AdminCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
