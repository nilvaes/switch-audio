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
    private readonly SwitchOsd _osd = new();
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

        // Left-click shows which output is playing; double-click switches; right-click opens the menu.
        // Windows delivers the first click of a double-click as a normal click, so a double-click
        // briefly shows the current device and then the new one — no delay needed to tell them apart.
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowCurrentDevice();
        };
        _trayIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                PerformSwitch();
        };

        // Refresh the tooltip as the mouse arrives, so it is right even if the
        // device was changed in Windows' own sound settings.
        _trayIcon.MouseMove += (_, _) => UpdateTooltip(throttle: true);
        UpdateTooltip();

        if (!RegisterHotkey())
            ShowTrayWarning($"Could not register {CurrentHotkeyLabel()} — another app may be using it.");
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

        // Release downloads have the version in the file name, so after an update the
        // startup entry would still launch the old exe. Point it at the one running now.
        if (_settings.StartWithWindows)
        {
            try
            {
                StartupManager.SetEnabled(true);
            }
            catch
            {
                // Not critical — the menu toggle reports errors if the user changes it.
            }
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, e) =>
        {
            RefreshMenu(menu);
            // WinForms pre-cancels opening a menu that is empty — which this one is on the first
            // right-click, before RefreshMenu fills it. Without this the first right-click does nothing.
            e.Cancel = false;
        };
        return menu;
    }

    private void RefreshMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        var devices = _audioManager.GetPlaybackDevices();
        string? currentId = _audioManager.GetDefaultDeviceId();

        if (_settings.IsConfigured)
        {
            // The two favorites, with a checkmark on the one that's playing. Click to switch.
            menu.Items.Add(CreateDeviceItem(_settings.DeviceAId!, _settings.DeviceAName, devices, currentId));
            menu.Items.Add(CreateDeviceItem(_settings.DeviceBId!, _settings.DeviceBName, devices, currentId));

            if (currentId != _settings.DeviceAId && currentId != _settings.DeviceBId)
            {
                string currentName = devices.FirstOrDefault(d => d.Id == currentId)?.Name ?? "Unknown";
                menu.Items.Add(new ToolStripMenuItem($"Now playing on: {EscapeMenuText(currentName)}") { Enabled = false });
            }
        }
        else
        {
            menu.Items.Add(new ToolStripMenuItem("No devices chosen yet") { Enabled = false });
        }

        menu.Items.Add(new ToolStripSeparator());

        var chooseDevicesItem = new ToolStripMenuItem("Choose devices…");
        chooseDevicesItem.Click += (_, _) => ShowDevicePicker();
        menu.Items.Add(chooseDevicesItem);

        menu.Items.Add(new ToolStripSeparator());

        var hotkeyItem = new ToolStripMenuItem($"Hotkey: {CurrentHotkeyLabel()}");
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

        var notificationsItem = new ToolStripMenuItem("Show switch popup")
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

    private ToolStripMenuItem CreateDeviceItem(
        string deviceId,
        string? savedName,
        IReadOnlyList<AudioDevice> devices,
        string? currentId)
    {
        var device = devices.FirstOrDefault(d => d.Id == deviceId);
        string name = EscapeMenuText(device?.Name ?? savedName ?? "Unknown device");

        var item = new ToolStripMenuItem(device is null ? $"{name} (unavailable)" : name)
        {
            Checked = deviceId == currentId,
            Enabled = device is not null
        };
        item.Click += (_, _) => SwitchTo(deviceId);
        return item;
    }

    // "&" in menu text marks a keyboard shortcut; double it so device names show as-is.
    private static string EscapeMenuText(string text) => text.Replace("&", "&&");

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

        uint oldModifiers = _settings.HotkeyModifiers;
        int oldKey = _settings.HotkeyKey;

        _settings.HotkeyModifiers = picker.Modifiers;
        _settings.HotkeyKey = (int)picker.Key;

        if (!RegisterHotkey())
        {
            // Keep the previous hotkey instead of ending up with none.
            string rejected = HotkeyManager.FormatHotkey(picker.Modifiers, picker.Key);
            _settings.HotkeyModifiers = oldModifiers;
            _settings.HotkeyKey = oldKey;
            RegisterHotkey();
            ShowTrayWarning($"{rejected} is already used by another app. Keeping {CurrentHotkeyLabel()}.");
            return;
        }

        _settings.Save();
    }

    private string CurrentHotkeyLabel() =>
        HotkeyManager.FormatHotkey(_settings.HotkeyModifiers, (Keys)_settings.HotkeyKey);

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

    private bool RegisterHotkey()
    {
        if (_hotkeyId > 0)
            _hotkeyManager.Unregister(_hotkeyId);

        _hotkeyId = _hotkeyManager.Register(_settings.HotkeyModifiers, (Keys)_settings.HotkeyKey);
        return _hotkeyId > 0;
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

        // If something other than A is playing (B, or a third device), go to A.
        string targetId = _audioManager.GetDefaultDeviceId() == _settings.DeviceAId
            ? _settings.DeviceBId!
            : _settings.DeviceAId!;

        SwitchTo(targetId);
    }

    // Shown even when switch popups are turned off, since the user asked by clicking.
    private void ShowCurrentDevice()
    {
        string? name = null;
        try
        {
            name = _audioManager.GetDefaultDevice()?.Name;
        }
        catch
        {
            // Fall through to the "no device" message.
        }

        _osd.ShowMessage(name ?? "No output device");
        SetTooltip(name);
    }

    private void SwitchTo(string deviceId)
    {
        try
        {
            _audioManager.SetDefaultDevice(deviceId);

            string name = _audioManager.GetPlaybackDevices().FirstOrDefault(d => d.Id == deviceId)?.Name
                ?? "Unknown device";
            if (_settings.ShowNotifications)
                _osd.ShowMessage(name);

            // We already know the new device, so skip asking Windows again — keeps fast switching snappy.
            SetTooltip(name);
        }
        catch (Exception ex)
        {
            ShowTrayWarning(ex.Message);
        }
    }

    private DateTime _lastTooltipUpdate;

    private void UpdateTooltip(bool throttle = false)
    {
        // MouseMove fires constantly while hovering; asking Windows once a second is plenty.
        if (throttle && DateTime.UtcNow - _lastTooltipUpdate < TimeSpan.FromSeconds(1))
            return;
        _lastTooltipUpdate = DateTime.UtcNow;

        string? name = null;
        try
        {
            name = _audioManager.GetDefaultDevice()?.Name;
        }
        catch
        {
            // Leave the plain title if Windows can't be asked right now.
        }

        SetTooltip(name);
    }

    private void SetTooltip(string? deviceName)
    {
        // NotifyIcon.Text throws if it is too long; long Bluetooth names can hit the limit.
        string tooltip = deviceName is null ? "Switch Audio" : $"Switch Audio — {deviceName}";
        _trayIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..62] + "…";
        _lastTooltipUpdate = DateTime.UtcNow;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyManager.Dispose();
            _hotkeyWindow.Dispose();
            _osd.Dispose();
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
