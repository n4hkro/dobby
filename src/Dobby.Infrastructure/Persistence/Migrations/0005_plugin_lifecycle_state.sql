ALTER TABLE InstalledPluginState ADD COLUMN IsPendingUninstall INTEGER NOT NULL DEFAULT 0 CHECK (IsPendingUninstall IN (0,1));
ALTER TABLE InstalledPluginState ADD COLUMN DataRetentionChoice TEXT NOT NULL DEFAULT 'keep';
ALTER TABLE InstalledPluginState ADD COLUMN RestartRequired INTEGER NOT NULL DEFAULT 0 CHECK (RestartRequired IN (0,1));
ALTER TABLE InstalledPluginState ADD COLUMN LastActivatedAtUtc TEXT;
CREATE INDEX IF NOT EXISTS IX_InstalledPluginState_IsPendingUninstall ON InstalledPluginState(IsPendingUninstall);
CREATE INDEX IF NOT EXISTS IX_InstalledPluginState_RestartRequired ON InstalledPluginState(RestartRequired);

ALTER TABLE InstalledPluginMetadataCache ADD COLUMN IsPendingUninstall INTEGER NOT NULL DEFAULT 0 CHECK (IsPendingUninstall IN (0,1));
ALTER TABLE InstalledPluginMetadataCache ADD COLUMN RestartRequired INTEGER NOT NULL DEFAULT 0 CHECK (RestartRequired IN (0,1));
CREATE INDEX IF NOT EXISTS IX_InstalledPluginMetadataCache_IsPendingUninstall ON InstalledPluginMetadataCache(IsPendingUninstall);
CREATE INDEX IF NOT EXISTS IX_InstalledPluginMetadataCache_RestartRequired ON InstalledPluginMetadataCache(RestartRequired);
