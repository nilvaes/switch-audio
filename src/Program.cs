using SwitchAudio.UI;

namespace SwitchAudio;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Render crisply at any display scaling (otherwise Windows stretches the windows and they look blurry).
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Follow the Windows light/dark app setting. WinForms still marks this API as experimental.
#pragma warning disable WFO5001
        Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001

        // Only one copy may run: a second one would add a duplicate tray icon and
        // fail to register the hotkey the first copy already owns.
        using var singleInstance = new Mutex(true, @"Local\SwitchAudio.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "Switch Audio is already running. Look for its icon in the system tray.",
                "Switch Audio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
