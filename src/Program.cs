using SwitchAudio.UI;

namespace SwitchAudio;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

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
