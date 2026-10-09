using System.Text.Json;

namespace Tytan.Updater.Desktop;

// Only customer/product folder names are persisted. Credentials are never written here.
internal sealed record FolderPreferences(string ClientFolder, string InstalledFolder)
{
    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TytanUpdater", "folders.json");

    internal static FolderPreferences? Read(string path)
    {
        if (!File.Exists(path)) return null;
        var preferences = JsonSerializer.Deserialize<FolderPreferences>(File.ReadAllText(path));
        if (preferences is null || preferences.ClientFolder is null || preferences.InstalledFolder is null)
            throw new InvalidDataException("Saved folder settings are incomplete.");

        InstallationFileReader.FromFolderNames(preferences.ClientFolder, preferences.InstalledFolder);
        return preferences;
    }

    internal void Save(string path)
    {
        InstallationFileReader.FromFolderNames(ClientFolder, InstalledFolder);
        string target = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
