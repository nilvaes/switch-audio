using System.Text.Json;
using System.Text.Json.Serialization;
using SwitchAudio.Input;

namespace SwitchAudio.Settings;

public class AppSettings
{
    public string? DeviceAId { get; set; }
    public string? DeviceAName { get; set; }
    public string? DeviceBId { get; set; }
    public string? DeviceBName { get; set; }
    public uint HotkeyModifiers { get; set; } = HotkeyManager.ModControl | HotkeyManager.ModAlt;
    public int HotkeyKey { get; set; } = (int)System.Windows.Forms.Keys.S;

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrEmpty(DeviceAId) && !string.IsNullOrEmpty(DeviceBId);

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SwitchAudio",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt file — start fresh.
        }

        return new AppSettings();
    }

    public void Save()
    {
        string directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(SettingsPath, json);
    }
}
