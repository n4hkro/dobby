namespace Dobby.Licensing;

public enum LicenseLifecycleState
{
    NoLicense,
    Pending,
    Active,
    Grace,
    ReadOnly,
    Denied
}
