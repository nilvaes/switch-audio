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
        SyncStartupSettingWithRegistry();

        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;

        // Show() creates the window handle Windows needs for RegisterHotKey.
        _hotkeyWindow.Show();
        _hotkeyWindow.Visible = false;

        _hotkeyManager = new HotkeyManager(_hotkeyWindow.Handle);

        _trayIcon = new NotifyIcon
        {
            Text = "Switch Audio",
            Icon = AppIcon.GetTrayIcon(),
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        RegisterHotkey();
        WarnIfSavedDevicesMissing();

        if (!_settings.IsConfigured)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                "Switch Audio",
                "Right-click the tray icon and choose \"Choose devices…\".",
                ToolTipIcon.Info);
        }
    }

    private void SyncStartupSettingWithRegistry()
    {
        _settings.StartWithWindows = StartupManager.IsEnabled();
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

        string deviceSummary = _settings.IsConfigured
            ? $"{_settings.DeviceAName} ↔ {_settings.DeviceBName}"
            : "not configured";
        menu.Items.Add(new ToolStripLabel($"Favorites: {deviceSummary}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        var chooseDevicesItem = new ToolStripMenuItem("Choose devices…");
        chooseDevicesItem.Click += (_, _) => ShowDevicePicker();
        menu.Items.Add(chooseDevicesItem);

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

        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = _settings.StartWithWindows,
            CheckOnClick = true
        };
        startupItem.Click += (_, _) => ToggleStartup(startupItem);
        menu.Items.Add(startupItem);

        var notificationsItem = new ToolStripMenuItem("Show switch notifications")
        {
            Checked = _settings.ShowNotifications,
            CheckOnClick = true
        };
        notificationsItem.Click += (_, _) =>
        {
            _settings.ShowNotifications = notificationsItem.Checked;
            _settings.Save();
        };
        menu.Items.Add(notificationsItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            ExitThread();
        };
        menu.Items.Add(exitItem);
    }

    private void ShowDevicePicker()
    {
        var devices = _audioManager.GetPlaybackDevices();
        if (devices.Count < 2)
        {
            ShowTrayWarning("You need at least 2 audio output devices.");
            return;
        }

        using var picker = new DevicePickerForm(devices, _settings.DeviceAId, _settings.DeviceBId);
        if (picker.ShowDialog() != DialogResult.OK)
            return;

        _settings.DeviceAId = picker.SelectedDeviceAId;
        _settings.DeviceAName = picker.SelectedDeviceAName;
        _settings.DeviceBId = picker.SelectedDeviceBId;
        _settings.DeviceBName = picker.SelectedDeviceBName;
        _settings.Save();

        _trayIcon.ShowBalloonTip(
            2000,
            "Switch Audio",
            $"Saved: {picker.SelectedDeviceAName} ↔ {picker.SelectedDeviceBName}",
            ToolTipIcon.Info);
    }

    private void ToggleStartup(ToolStripMenuItem startupItem)
    {
        try
        {
            StartupManager.SetEnabled(startupItem.Checked);
            _settings.StartWithWindows = startupItem.Checked;
            _settings.Save();
        }
        catch (Exception ex)
        {
            startupItem.Checked = !startupItem.Checked;
            ShowTrayWarning(ex.Message);
        }
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
    }

    private void WarnIfSavedDevicesMissing()
    {
        if (!_settings.IsConfigured)
            return;

        var devices = _audioManager.GetPlaybackDevices();
        bool hasA = devices.Any(d => d.Id == _settings.DeviceAId);
        bool hasB = devices.Any(d => d.Id == _settings.DeviceBId);

        if (!hasA || !hasB)
            ShowTrayWarning("A saved device is unplugged or unavailable. Pick devices again.");
    }

    private void ShowTrayWarning(string message)
    {
        _trayIcon.ShowBalloonTip(4000, "Switch Audio", message, ToolTipIcon.Warning);
    }

    private void RegisterHotkey()
    {
        if (_hotkeyId > 0)
            _hotkeyManager.Unregister(_hotkeyId);

        _hotkeyId = _hotkeyManager.Register(_settings.HotkeyModifiers, (Keys)_settings.HotkeyKey);
        if (_hotkeyId < 0)
            ShowTrayWarning("Could not register hotkey — another app may be using it.");
    }

    private void OnHotkeyPressed()
    {
        if (!HotkeyManager.AreModifiersPhysicallyPressed(_settings.HotkeyModifiers))
            return;

        PerformSwitch();
    }

    private void PerformSwitch()
    {
        if (!_settings.IsConfigured)
        {
            ShowTrayWarning("Choose Device A and Device B first.");
            return;
        }

        var devices = _audioManager.GetPlaybackDevices();
        if (!devices.Any(d => d.Id == _settings.DeviceAId) ||
            !devices.Any(d => d.Id == _settings.DeviceBId))
        {
            ShowTrayWarning("A saved device is unplugged or unavailable. Pick devices again.");
            return;
        }

        try
        {
            var device = _audioManager.Toggle(_settings.DeviceAId!, _settings.DeviceBId!);
            if (_settings.ShowNotifications)
                _trayIcon.ShowBalloonTip(2000, "Switch Audio", $"Now: {device.Name}", ToolTipIcon.None);
            _trayIcon.Text = $"Switch Audio — {device.Name}";
        }
        catch (Exception ex)
        {
            ShowTrayWarning(ex.Message);
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
