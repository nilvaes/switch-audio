using SwitchAudio.Audio;
using SwitchAudio.Input;
using SwitchAudio.Settings;

namespace SwitchAudio.UI;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly AudioManager _audioManager = new();
    private readonly AppSettings _settings;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly HotkeyManager _hotkeyManager;
    private int _hotkeyId = -1;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _hotkeyWindow = new HotkeyWindow();
        _hotkeyManager = new HotkeyManager(_hotkeyWindow.Handle);
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;

        _trayIcon = new NotifyIcon
        {
            Text = "Switch Audio",
            Icon = SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _hotkeyWindow.CreateControl();
        RegisterHotkey();

        if (!_settings.IsConfigured)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                "Switch Audio",
                "Right-click the tray icon to pick Device A and Device B.",
                ToolTipIcon.Info);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => RefreshMenu(menu);
        return menu;
    }

    private void RefreshMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        var current = _audioManager.GetDefaultDevice();
        menu.Items.Add(new ToolStripLabel($"Current: {current?.Name ?? "Unknown"}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        var deviceAMenu = new ToolStripMenuItem($"Device A: {_settings.DeviceAName ?? "not set"}");
        var deviceBMenu = new ToolStripMenuItem($"Device B: {_settings.DeviceBName ?? "not set"}");

        foreach (var device in _audioManager.GetPlaybackDevices())
        {
            deviceAMenu.DropDownItems.Add(CreateDeviceMenuItem(device, slot: 'A'));
            deviceBMenu.DropDownItems.Add(CreateDeviceMenuItem(device, slot: 'B'));
        }

        menu.Items.Add(deviceAMenu);
        menu.Items.Add(deviceBMenu);
        menu.Items.Add(new ToolStripSeparator());

        var switchItem = new ToolStripMenuItem("Switch now")
        {
            Enabled = _settings.IsConfigured
        };
        switchItem.Click += (_, _) => PerformSwitch();
        menu.Items.Add(switchItem);

        menu.Items.Add(new ToolStripSeparator());

        var hotkeyLabel = HotkeyManager.FormatHotkey(_settings.HotkeyModifiers, (Keys)_settings.HotkeyKey);
        var hotkeyItem = new ToolStripMenuItem($"Hotkey: {hotkeyLabel}");
        hotkeyItem.Click += (_, _) => ChangeHotkey();
        menu.Items.Add(hotkeyItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            ExitThread();
        };
        menu.Items.Add(exitItem);
    }

    private ToolStripMenuItem CreateDeviceMenuItem(AudioDevice device, char slot)
    {
        bool isDeviceA = slot == 'A';
        bool selected = isDeviceA
            ? device.Id == _settings.DeviceAId
            : device.Id == _settings.DeviceBId;

        var item = new ToolStripMenuItem(device.Name) { Checked = selected };
        item.Click += (_, _) =>
        {
            if (isDeviceA)
            {
                _settings.DeviceAId = device.Id;
                _settings.DeviceAName = device.Name;
            }
            else
            {
                _settings.DeviceBId = device.Id;
                _settings.DeviceBName = device.Name;
            }

            _settings.Save();
            _trayIcon.ContextMenuStrip = BuildMenu();
        };

        return item;
    }

    private void ChangeHotkey()
    {
        using var picker = new HotkeyPickerForm(_settings.HotkeyModifiers, (Keys)_settings.HotkeyKey);
        if (picker.ShowDialog() != DialogResult.OK || !picker.Confirmed)
            return;

        _settings.HotkeyModifiers = picker.Modifiers;
        _settings.HotkeyKey = (int)picker.Key;
        _settings.Save();
        RegisterHotkey();
        _trayIcon.ContextMenuStrip = BuildMenu();
    }

    private void RegisterHotkey()
    {
        if (_hotkeyId > 0)
            _hotkeyManager.Unregister(_hotkeyId);

        _hotkeyId = _hotkeyManager.Register(_settings.HotkeyModifiers, (Keys)_settings.HotkeyKey);
        if (_hotkeyId < 0)
        {
            _trayIcon.ShowBalloonTip(
                4000,
                "Switch Audio",
                "Could not register hotkey — another app may be using it.",
                ToolTipIcon.Warning);
        }
    }

    private void OnHotkeyPressed() => PerformSwitch();

    private void PerformSwitch()
    {
        if (!_settings.IsConfigured)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                "Switch Audio",
                "Pick Device A and Device B from the tray menu first.",
                ToolTipIcon.Warning);
            return;
        }

        try
        {
            var device = _audioManager.Toggle(_settings.DeviceAId!, _settings.DeviceBId!);
            _trayIcon.ShowBalloonTip(2000, "Switch Audio", $"Now: {device.Name}", ToolTipIcon.None);
            _trayIcon.Text = $"Switch Audio — {device.Name}";
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(4000, "Switch Audio", ex.Message, ToolTipIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyManager.Dispose();
            _hotkeyWindow.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
