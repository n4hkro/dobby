CREATE TABLE IF NOT EXISTS InstalledPluginVersion (
    PluginId TEXT NOT NULL,
    PackageFingerprint TEXT NOT NULL,
    Version TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Description TEXT NOT NULL,
    RuntimePath TEXT NOT NULL,
    ValidationSummary TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL CHECK (IsEnabled IN (0,1)),
    InstalledAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY (PluginId, PackageFingerprint),
    UNIQUE (RuntimePath)
);
CREATE INDEX IF NOT EXISTS IX_InstalledPluginVersion_PluginId
    ON InstalledPluginVersion(PluginId);

CREATE TABLE IF NOT EXISTS InstalledPluginVersionPointer (
    PluginId TEXT PRIMARY KEY,
    ActiveFingerprint TEXT NOT NULL,
    LastKnownGoodFingerprint TEXT NOT NULL,
    PendingActivationFingerprint TEXT,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (PluginId, ActiveFingerprint)
        REFERENCES InstalledPluginVersion(PluginId, PackageFingerprint),
    FOREIGN KEY (PluginId, LastKnownGoodFingerprint)
        REFERENCES InstalledPluginVersion(PluginId, PackageFingerprint),
    FOREIGN KEY (PluginId, PendingActivationFingerprint)
        REFERENCES InstalledPluginVersion(PluginId, PackageFingerprint)
);

CREATE TABLE IF NOT EXISTS PluginInstallOperation (
    OperationId TEXT PRIMARY KEY,
    PluginId TEXT NOT NULL,
    Version TEXT NOT NULL,
    PackageFingerprint TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Description TEXT NOT NULL,
    StagedPath TEXT NOT NULL,
    RuntimePath TEXT NOT NULL,
    State TEXT NOT NULL CHECK (State IN (
        'Staged', 'Verified', 'ReadyToPromote', 'Promoted',
        'StateCommitted', 'Completed')),
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    UNIQUE (StagedPath),
    UNIQUE (RuntimePath, OperationId)
);
CREATE INDEX IF NOT EXISTS IX_PluginInstallOperation_PluginIdState
    ON PluginInstallOperation(PluginId, State);
CREATE INDEX IF NOT EXISTS IX_PluginInstallOperation_UpdatedAtUtc
    ON PluginInstallOperation(UpdatedAtUtc);

INSERT OR IGNORE INTO InstalledPluginVersion (
    PluginId, PackageFingerprint, Version, DisplayName, Description,
    RuntimePath, ValidationSummary, IsEnabled, InstalledAtUtc, UpdatedAtUtc)
SELECT
    PluginId, PackageFingerprint, Version, DisplayName, Description,
    RuntimePath, ValidationSummary, IsEnabled, InstalledAtUtc, UpdatedAtUtc
FROM InstalledPluginState;

INSERT OR IGNORE INTO InstalledPluginVersionPointer (
    PluginId, ActiveFingerprint, LastKnownGoodFingerprint,
    PendingActivationFingerprint, UpdatedAtUtc)
SELECT
    PluginId, PackageFingerprint, PackageFingerprint, NULL, UpdatedAtUtc
FROM InstalledPluginState;
