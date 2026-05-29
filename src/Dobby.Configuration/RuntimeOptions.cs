namespace Dobby.Configuration;

public sealed class RuntimeOptions
{
    public string DataRoot { get; init; } = DefaultDataRoot();

    public RuntimeEnvironmentName EnvironmentName { get; init; } = RuntimeEnvironmentName.Production;

    private static string DefaultDataRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData)
            ? "%LocalAppData%/Dobby"
            : Path.Combine(localAppData, "Dobby");
    }
}
