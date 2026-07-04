using Dobby.Core;

namespace Dobby.Configuration;

public interface IThemePreferenceSource
{
    Result SaveThemePreference(AppThemePreference theme);
}
