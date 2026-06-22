using Microsoft.Win32;

namespace SwitchAudio.Settings;

public static class StartupManager
{
    private const string AppName = "SwitchAudio";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not open Windows startup registry key.");

        if (enabled)
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                throw new InvalidOperationException("Could not find the app executable path.");

            key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
