ALTER TABLE PluginLicenseState ADD COLUMN NotBeforeUtc TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN ExpiresAtUtc TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN GracePeriodDays INTEGER;
ALTER TABLE PluginLicenseState ADD COLUMN SourceStatus TEXT;
