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
                "--overwrite"
            ]),
        [CreateRecoveryResponse] = new(
            [
                "--request-path",
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
        return Run(args, new UnavailableAdminSigningMaterialProvider(), TimeProvider.System);
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
            }

            if (File.Exists(outputPath) && !overwrite)
            {
                return Validation("output-conflict");
            }

            if (File.Exists(reportPath) && !overwrite)
            {
                return Validation("report-conflict");
            }

            var material = signingMaterialProvider.Resolve(signingProfile);
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

        var read = PluginPackageReader.Read(inputPath);
        if (!read.IsSuccess)
        {
            return Validation("invalid-package");
        }

        var archive = read.Value;
        var fingerprint = PluginPackageFingerprint.Compute(archive.Manifest);
        var signature = ToBase64Url(material.Key.SignData(
            fingerprint.CanonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));
        var manifestJson = WritePluginManifestJson(archive.Manifest, signature);

        EnsureParentDirectory(outputPath);
        using (var stream = File.Create(outputPath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            AddZipEntry(zip, PluginPackageReader.ManifestFileName, Encoding.UTF8.GetBytes(manifestJson));
            foreach (var entry in archive.Entries
                         .Where(entry => !entry.Path.Equals(archive.ManifestPath, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(entry => entry.Path, StringComparer.Ordinal))
            {
                AddZipEntry(zip, entry.Path, entry.Content);
            }
        }

        var report = new
        {
            command = SignPluginPackage,
            signingProfile = material.Profile,
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
        var payload = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customerId"] = Require(options, "--customer-id"),
            ["deploymentId"] = Require(options, "--deployment-id"),
            ["deviceFingerprint"] = deviceFingerprint,
            ["deviceFingerprintAlgorithm"] = "v1",
            ["expiresAt"] = ToUtcZ(validUntil),
            ["gracePeriodDays"] = 14,
            ["issuedAt"] = ToUtcZ(validFrom),
            ["keyVersion"] = 1,
            ["licenseId"] = Guid.NewGuid().ToString("D"),
            ["licenseRevision"] = revision,
            ["notBefore"] = ToUtcZ(validFrom),
            ["packageFingerprint"] = packageFingerprint,
            ["pluginId"] = pluginId,
            ["schemaVersion"] = 2,
            ["status"] = "active"
        };

        using var payloadDocument = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var canonicalPayload = LicenseCanonicalJsonWriter.Write(payloadDocument.RootElement);
        var signature = ToBase64Url(material.Key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence));
        var payloadJson = Encoding.UTF8.GetString(canonicalPayload);
        var artifactJson = "{\"payload\":" + payloadJson +
            ",\"signature\":{\"algorithm\":\"ECDSA-P256-SHA256\",\"keyVersion\":1,\"value\":\"" +
            signature +
            "\"}}";

        EnsureParentDirectory(outputPath);
        File.WriteAllText(outputPath, artifactJson, new UTF8Encoding(false));

        var report = new
        {
            command = CreatePluginLicense,
            signingProfile = material.Profile,
            outputFileName = Path.GetFileName(outputPath),
            reportFileName = Path.GetFileName(reportPath),
            pluginId,
            customerIdHash = Sha256Text(Require(options, "--customer-id"))[..12],
            deploymentIdHash = Sha256Text(Require(options, "--deployment-id"))[..12],
            licenseRevision = revision,
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

        var request = JsonSerializer.Deserialize<RecoveryRequestPayload>(
            File.ReadAllText(requestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (request is null ||
            !request.Type.Equals("dobby.owner-recovery-request.v1", StringComparison.Ordinal) ||
            request.DeviceIdentity is null ||
            string.IsNullOrWhiteSpace(request.RequestId))
        {
            return Validation("invalid-recovery-request");
        }

        var issuedAt = timeProvider.GetUtcNow();
        var payload = new RecoveryResponsePayload(
            "dobby.owner-recovery-response.v1",
            request.RequestId,
            request.DeviceIdentity,
            ToUtcZ(issuedAt),
            ToUtcZ(issuedAt.AddHours(24)),
            Guid.NewGuid().ToString("N"),
            1);
        var canonicalPayload = RecoveryCanonicalJsonWriter.WritePayload(payload);
        var signature = Convert.ToBase64String(material.Key.SignData(
            canonicalPayload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var envelope = new RecoveryResponseEnvelope(payload, signature);

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

    private static string WritePluginManifestJson(PluginManifest manifest, string signature)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("pluginId", manifest.PluginId);
            writer.WriteString("version", manifest.Version);
            writer.WriteString("vendorId", manifest.VendorId);
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
            writer.WriteString("algorithm", "ECDSA-P256-SHA256");
            writer.WriteNumber("keyVersion", 1);
            writer.WriteString("value", signature);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void AddZipEntry(ZipArchive zip, string path, byte[] content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(content);
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
    AdminSigningMaterialResult Resolve(string signingProfile);
}

public sealed record AdminSigningMaterial(
    string Profile,
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
    public AdminSigningMaterialResult Resolve(string signingProfile)
    {
        return AdminSigningMaterialResult.Unavailable("signing-material-unavailable");
    }
}

public sealed record AdminCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
