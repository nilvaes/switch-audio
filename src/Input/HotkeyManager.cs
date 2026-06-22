using System.Runtime.InteropServices;

namespace SwitchAudio.Input;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;

    public const int WmHotkey = 0x0312;

    private readonly IntPtr _handle;
    private int _nextId = 1;

    public HotkeyManager(IntPtr windowHandle)
    {
        _handle = windowHandle;
    }

    public int Register(uint modifiers, Keys key)
    {
        int id = _nextId++;
        bool success = RegisterHotKey(_handle, id, modifiers | ModNoRepeat, (uint)key);
        return success ? id : -1;
    }

    public void Unregister(int id)
    {
        if (id > 0)
            UnregisterHotKey(_handle, id);
    }

    public void Dispose()
    {
        for (int i = 1; i < _nextId; i++)
            UnregisterHotKey(_handle, i);
    }

    public static string FormatHotkey(uint modifiers, Keys key)
    {
        var parts = new List<string>();
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & ModShift) != 0) parts.Add("Shift");
        if ((modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join(" + ", parts);
    }
}
