using Dobby.Configuration;
using Dobby.Diagnostics;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System.Globalization;
using System.IO;

namespace Dobby.Host;

public static class LocalDiagnosticsConfigurator
{
    public static Logger CreateLogger(
        RuntimeOptions runtimeOptions,
        DiagnosticsOptions diagnosticsOptions,
        string minimumLevel)
    {
        return CreateLoggerCore(
            runtimeOptions,
            diagnosticsOptions,
            BuildMinimumLevelConfiguration(minimumLevel));
    }

    public static Logger CreateLogger(
        RuntimeOptions runtimeOptions,
        DiagnosticsOptions diagnosticsOptions,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return CreateLoggerCore(
            runtimeOptions,
            diagnosticsOptions,
            BuildMinimumLevelConfiguration(configuration));
    }

    private static Logger CreateLoggerCore(
        RuntimeOptions runtimeOptions,
        DiagnosticsOptions diagnosticsOptions,
        IConfiguration minimumLevelConfiguration)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        ArgumentNullException.ThrowIfNull(diagnosticsOptions);

        if (diagnosticsOptions.TechnicalLogRetentionDays < 30)
        {
            throw new InvalidOperationException("Diagnostics retention cannot be below 30 days.");
        }

        if (diagnosticsOptions.TechnicalLogTotalSizeCapMegabytes <= 0)
        {
            throw new InvalidOperationException("Diagnostics total size cap must be finite.");
        }

        var logsDirectory = Path.Combine(Environment.ExpandEnvironmentVariables(runtimeOptions.DataRoot), "logs");
        Directory.CreateDirectory(logsDirectory);

        var fileSizeLimitBytes = diagnosticsOptions.TechnicalLogTotalSizeCapMegabytes * 1024L * 1024L;
        EnforceTotalSizeCap(logsDirectory, fileSizeLimitBytes);

        return new LoggerConfiguration()
            .ReadFrom.Configuration(minimumLevelConfiguration)
            .WriteTo.File(
                new RedactingTextFormatter(),
                Path.Combine(logsDirectory, "dobby-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: diagnosticsOptions.TechnicalLogRetentionDays,
                fileSizeLimitBytes: fileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                buffered: false,
                shared: true)
            .CreateLogger();
    }

    private static IConfiguration BuildMinimumLevelConfiguration(string minimumLevel)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel"] = string.IsNullOrWhiteSpace(minimumLevel)
                    ? LogEventLevel.Information.ToString()
                    : minimumLevel
            })
            .Build();
    }

    private static IConfiguration BuildMinimumLevelConfiguration(IConfiguration configuration)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        CopyMinimumLevel(configuration.GetSection("Serilog:MinimumLevel"), "Serilog:MinimumLevel", values);

        if (values.Count == 0)
        {
            values["Serilog:MinimumLevel"] = LogEventLevel.Information.ToString();
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static void CopyMinimumLevel(
        IConfigurationSection section,
        string targetKey,
        Dictionary<string, string?> values)
    {
        if (section.Value is not null)
        {
            values[targetKey] = section.Value;
        }

        foreach (var child in section.GetChildren())
        {
            CopyMinimumLevel(child, $"{targetKey}:{child.Key}", values);
        }
    }

    private sealed class RedactingTextFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            output.Write("timestamp=");
            output.Write(logEvent.Timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            output.Write(" level=");
            output.Write(logEvent.Level);

            foreach (var property in logEvent.Properties.OrderBy(property => property.Key, StringComparer.Ordinal))
            {
                output.Write(' ');
                output.Write(ToCanonicalPropertyName(property.Key));
                output.Write('=');
                output.Write(DiagnosticRedactor.Redact(RenderPropertyValue(property.Value)));
            }

            output.Write(" messageTemplate=");
            output.Write(Quote(DiagnosticRedactor.Redact(logEvent.MessageTemplate.Text)));
            output.Write(" message=");
            output.Write(DiagnosticRedactor.Redact(Quote(logEvent.RenderMessage(CultureInfo.InvariantCulture))));
            if (logEvent.Exception is not null)
            {
                output.Write(" exception=");
                output.Write(DiagnosticRedactor.Redact(Quote(logEvent.Exception.Message)));
            }

            output.WriteLine();
        }

        private static string RenderPropertyValue(LogEventPropertyValue propertyValue)
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            propertyValue.Render(writer, format: "l", CultureInfo.InvariantCulture);
            return writer.ToString();
        }

        private static string ToCanonicalPropertyName(string propertyName)
        {
            return propertyName.Length == 0
                ? propertyName
                : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }
    }

    private static void EnforceTotalSizeCap(string logsDirectory, long totalSizeCapBytes)
    {
        var files = Directory
            .EnumerateFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
        var totalBytes = files.Sum(file => file.Length);

        foreach (var file in files.Reverse())
        {
            if (totalBytes <= totalSizeCapBytes)
            {
                break;
            }

            totalBytes -= file.Length;
            file.Delete();
        }
    }
}
