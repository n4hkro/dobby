CREATE TABLE IF NOT EXISTS InstalledPluginState (
    PluginId TEXT PRIMARY KEY,
    Version TEXT NOT NULL,
    PackageFingerprint TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Description TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL CHECK (IsEnabled IN (0,1)),
    ValidationSummary TEXT NOT NULL,
    RuntimePath TEXT NOT NULL,
    StatusSummary TEXT NOT NULL,
    InstalledAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_InstalledPluginState_IsEnabled ON InstalledPluginState(IsEnabled);
CREATE INDEX IF NOT EXISTS IX_InstalledPluginState_PackageFingerprint ON InstalledPluginState(PackageFingerprint);

CREATE TABLE IF NOT EXISTS InstalledPluginMetadataCache (
    PluginId TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL,
    Version TEXT NOT NULL,
    Description TEXT NOT NULL,
    PackageFingerprint TEXT NOT NULL,
    IsEnabled INTEGER NOT NULL CHECK (IsEnabled IN (0,1)),
    StatusSummary TEXT NOT NULL,
    InstalledAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_InstalledPluginMetadataCache_IsEnabled ON InstalledPluginMetadataCache(IsEnabled);

CREATE TABLE IF NOT EXISTS PluginInstallStaging (
    StagingId TEXT PRIMARY KEY,
    PluginId TEXT NOT NULL,
    PackageFingerprint TEXT NOT NULL,
    StagedPath TEXT NOT NULL,
    RuntimePath TEXT,
    State TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_PluginInstallStaging_PluginId ON PluginInstallStaging(PluginId);
CREATE INDEX IF NOT EXISTS IX_PluginInstallStaging_State ON PluginInstallStaging(State);
