namespace Dobby.Licensing;

public interface ILicenseStateStore
{
    LicenseStateRecord? Read(RevisionSeriesKey key);

    void Save(LicenseStateRecord state);

    void AppendAttestation(LicenseImportAttestation attestation);
}
