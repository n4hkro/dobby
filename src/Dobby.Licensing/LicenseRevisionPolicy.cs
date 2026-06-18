namespace Dobby.Licensing;

public static class LicenseRevisionPolicy
{
    public static bool CanImport(int? existingRevision, int incomingRevision)
    {
        return incomingRevision > 0 && (existingRevision is null || incomingRevision > existingRevision);
    }
}
