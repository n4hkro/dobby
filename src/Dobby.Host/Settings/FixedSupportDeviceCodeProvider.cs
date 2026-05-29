namespace Dobby.Host.Settings;

public sealed class FixedSupportDeviceCodeProvider(string redactedCode) : ISupportDeviceCodeProvider
{
    public string CurrentRedactedCode()
    {
        return redactedCode;
    }
}
