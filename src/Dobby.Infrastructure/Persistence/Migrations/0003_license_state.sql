CREATE TABLE IF NOT EXISTS PluginLicenseState (
    PluginId TEXT NOT NULL,
    CustomerId TEXT NOT NULL,
    DeploymentId TEXT NOT NULL,
    DeviceFingerprint TEXT NOT NULL,
    PackageFingerprint TEXT NOT NULL,
    LicenseRevision INTEGER NOT NULL CHECK (LicenseRevision >= 1),
    LifecycleState TEXT NOT NULL,
    ImportedAtUtc TEXT NOT NULL,
    PRIMARY KEY (PluginId, CustomerId, DeploymentId)
);
CREATE INDEX IF NOT EXISTS IX_PluginLicenseState_PluginId ON PluginLicenseState(PluginId);
CREATE INDEX IF NOT EXISTS IX_PluginLicenseState_LifecycleState ON PluginLicenseState(LifecycleState);

CREATE TABLE IF NOT EXISTS PluginLicenseImportAttestation (
    AttestationId TEXT PRIMARY KEY,
    SourceArtifactHash TEXT NOT NULL,
    LicenseRevision INTEGER,
    ImportedAtUtc TEXT NOT NULL,
    Result TEXT NOT NULL,
    CorrelationId TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_PluginLicenseImportAttestation_ImportedAtUtc ON PluginLicenseImportAttestation(ImportedAtUtc);
CREATE INDEX IF NOT EXISTS IX_PluginLicenseImportAttestation_CorrelationId ON PluginLicenseImportAttestation(CorrelationId);
