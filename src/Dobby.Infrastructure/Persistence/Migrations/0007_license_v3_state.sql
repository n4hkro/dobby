ALTER TABLE PluginLicenseState ADD COLUMN LicenseId TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN RevisionSeriesId TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN KeyId TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN IssuedAtUtc TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN SourceIntent TEXT;
ALTER TABLE PluginLicenseState ADD COLUMN TrustedTimeHealth TEXT NOT NULL DEFAULT 'Healthy';

CREATE UNIQUE INDEX IF NOT EXISTS UX_PluginLicenseState_RevisionSeriesId
ON PluginLicenseState(RevisionSeriesId)
WHERE RevisionSeriesId IS NOT NULL;
