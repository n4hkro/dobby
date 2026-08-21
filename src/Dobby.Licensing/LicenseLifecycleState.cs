namespace Dobby.Licensing;

public enum LicenseLifecycleState
{
    NoLicense,
    Pending,
    Active,
    Grace,
    ReadOnlyExpired,
    ReadOnly = ReadOnlyExpired,
    Denied
}
