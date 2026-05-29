using Dobby.Configuration;
using Dobby.Core;
using Dobby.Infrastructure.ProtectedStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

namespace Dobby.Host;

public sealed class RuntimeFolderPreparer
{
    private readonly Func<RuntimeOptions, bool> protectedMaterialPrerequisiteAvailable;
    private readonly ILogger<RuntimeFolderPreparer> logger;

    public RuntimeFolderPreparer()
        : this(
            options => ProtectedMaterialAvailabilityProbe.CreateForDataRoot(options.DataRoot).IsAvailable(),
            NullLogger<RuntimeFolderPreparer>.Instance)
    {
    }

    public RuntimeFolderPreparer(Func<bool> protectedMaterialPrerequisiteAvailable)
        : this(_ => protectedMaterialPrerequisiteAvailable(), NullLogger<RuntimeFolderPreparer>.Instance)
    {
    }

    public RuntimeFolderPreparer(
        Func<bool> protectedMaterialPrerequisiteAvailable,
        ILogger<RuntimeFolderPreparer> logger)
        : this(_ => protectedMaterialPrerequisiteAvailable(), logger)
    {
    }

    public RuntimeFolderPreparer(
        Func<RuntimeOptions, bool> protectedMaterialPrerequisiteAvailable,
        ILogger<RuntimeFolderPreparer> logger)
    {
        this.protectedMaterialPrerequisiteAvailable = protectedMaterialPrerequisiteAvailable;
        this.logger = logger;
    }

    public void Prepare(RuntimeOptions options, CorrelationId correlationId)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!protectedMaterialPrerequisiteAvailable(options))
        {
            throw new InvalidOperationException("Required protected material prerequisite is missing; startup fails closed.");
        }

        foreach (var directory in RuntimeFolderLayout.RequiredDirectories(options))
        {
            Directory.CreateDirectory(directory);
            if (!Directory.Exists(directory))
            {
                throw new InvalidOperationException($"Required runtime folder was not created: {directory}");
            }

            logger.LogInformation("Runtime folder ready: {RuntimeFolder} {CorrelationId}", directory, correlationId.ToString());
        }
    }
}
