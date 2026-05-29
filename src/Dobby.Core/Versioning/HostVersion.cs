namespace Dobby.Core.Versioning;

public readonly record struct HostVersion(int Major, int Minor, int Patch) : IComparable<HostVersion>
{
    public static bool TryParse(string value, out HostVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('-', StringComparison.Ordinal) ||
            value.Contains('+', StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 3 ||
            !TryParseNumericComponent(parts[0], out var major) ||
            !TryParseNumericComponent(parts[1], out var minor) ||
            !TryParseNumericComponent(parts[2], out var patch))
        {
            return false;
        }

        version = new HostVersion(major, minor, patch);
        return true;
    }

    private static bool TryParseNumericComponent(string component, out int value)
    {
        value = default;

        if (component.Length == 0 ||
            (component.Length > 1 && component[0] == '0'))
        {
            return false;
        }

        foreach (var character in component)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(component, out value);
    }

    public int CompareTo(HostVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

public interface IHostVersionProvider
{
    HostVersion Current { get; }
}

public sealed class FixedHostVersionProvider(HostVersion current) : IHostVersionProvider
{
    public HostVersion Current { get; } = current;
}
