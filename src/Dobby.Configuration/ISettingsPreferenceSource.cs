using Dobby.Core;

namespace Dobby.Configuration;

public interface ISettingsPreferenceSource
{
    Result<SettingsPreferences> Load();

    Result Save(SettingsPreferences preferences);
}
