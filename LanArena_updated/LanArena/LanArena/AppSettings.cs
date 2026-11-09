using System.Text.Json;

namespace LanArena;

/// <summary>
/// Configuration settings for LAN Arena, including network and discovery preferences.
/// Persists settings to JSON file in AppData.
/// </summary>
public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LanArena",
        "settings.json"
    );

    public string ServerUrl { get; set; } = ServerDiscovery.DefaultServerUrl;
    public bool UseCloudDiscovery { get; set; } = true;
    public bool UseLanDiscovery { get; set; } = true;
    public int CloudPollingIntervalMs { get; set; } = 2000;
    public int GamePort { get; set; } = 47778;
    public int UdpPort { get; set; } = 47777;
    public string LastPlayerName { get; set; } = Environment.UserName;
    public string LastGameType { get; set; } = "TicTacToe";

    /// <summary>Load settings from file, or create defaults if not found</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }

        return new AppSettings();
    }

    /// <summary>Save settings to file</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    /// <summary>Reset to defaults</summary>
    public static void ResetToDefaults()
    {
        try
        {
            if (File.Exists(SettingsPath))
                File.Delete(SettingsPath);
        }
        catch { }
    }
}
