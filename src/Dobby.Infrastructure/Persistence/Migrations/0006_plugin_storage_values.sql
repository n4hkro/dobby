CREATE TABLE IF NOT EXISTS PluginStorageValue (
    PluginId TEXT NOT NULL,
    Scope TEXT NOT NULL CHECK (Scope IN ('shared-domain','user-scoped')),
    UserScopeHash TEXT NOT NULL DEFAULT '',
    StorageKey TEXT NOT NULL,
    Payload BLOB NOT NULL,
    ContentType TEXT,
    SchemaVersion INTEGER,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    PRIMARY KEY (PluginId, Scope, UserScopeHash, StorageKey)
);
CREATE INDEX IF NOT EXISTS IX_PluginStorageValue_PluginScope
    ON PluginStorageValue(PluginId, Scope, UserScopeHash);
