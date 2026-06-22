namespace SwitchAudio.UI;

// Invisible window that receives WM_HOTKEY messages from Windows.
// Global hotkeys need a window handle — the tray icon alone can't receive them.
internal class HotkeyWindow : Form
{
    public event Action? HotkeyPressed;

    public HotkeyWindow()
    {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.None;
        Opacity = 0;
        Size = new Size(0, 0);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Input.HotkeyManager.WmHotkey)
            HotkeyPressed?.Invoke();

        base.WndProc(ref m);
    }
}
