using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SwitchAudio.UI;

// Small popup near the bottom center of the screen that shows the device just switched to, then fades out —
// like the Windows volume popup. Replaces balloon notifications, which pile up in the
// notification center. It never takes focus and clicks pass through it.
internal sealed class SwitchOsd : Form
{
    private const int WsExTopmost = 0x00000008;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    private const double VisibleOpacity = 0.96;
    private const int VisibleMilliseconds = 1300;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private readonly Label _iconLabel;
    private readonly Label _textLabel;
    private readonly System.Windows.Forms.Timer _holdTimer = new() { Interval = VisibleMilliseconds };
    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = 15 };

    public SwitchOsd()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Opacity = 0;

        _iconLabel = new Label
        {
            AutoSize = true,
            Text = "", // Speaker glyph from the Windows icon font
            Font = new Font(GetIconFontFamily(), 16f),
            BackColor = Color.Transparent
        };

        _textLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 11f),
            BackColor = Color.Transparent
        };

        Controls.AddRange([_iconLabel, _textLabel]);

        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            _fadeTimer.Start();
        };

        _fadeTimer.Tick += (_, _) =>
        {
            Opacity = Math.Max(0, Opacity - 0.08);
            if (Opacity <= 0)
            {
                _fadeTimer.Stop();
                Hide();
            }
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExTopmost | WsExTransparent | WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Rounded corners on Windows 11; ignored on Windows 10.
        int preference = DwmwcpRound;
        try
        {
            DwmSetWindowAttribute(Handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
        }
    }

    public void ShowMessage(string text)
    {
        ApplyTheme();
        _textLabel.Text = text;
        LayoutContent();
        PlaceAboveTaskbar();

        _fadeTimer.Stop();
        _holdTimer.Stop();
        Opacity = VisibleOpacity;

        if (!Visible)
            Show();

        _holdTimer.Start();
    }

    private void LayoutContent()
    {
        int padX = LogicalToDeviceUnits(18);
        int padY = LogicalToDeviceUnits(12);
        int gap = LogicalToDeviceUnits(10);

        Size iconSize = _iconLabel.GetPreferredSize(Size.Empty);
        Size textSize = _textLabel.GetPreferredSize(Size.Empty);
        int contentHeight = Math.Max(iconSize.Height, textSize.Height);

        _iconLabel.Location = new Point(padX, padY + (contentHeight - iconSize.Height) / 2);
        _textLabel.Location = new Point(padX + iconSize.Width + gap, padY + (contentHeight - textSize.Height) / 2);

        int minWidth = LogicalToDeviceUnits(180);
        int width = Math.Max(minWidth, padX * 2 + iconSize.Width + gap + textSize.Width);
        ClientSize = new Size(width, padY * 2 + contentHeight);
    }

    private void PlaceAboveTaskbar()
    {
        // Centered near the bottom of the screen the mouse is on, raised a little above the
        // taskbar (the working area already excludes the taskbar, wherever it is docked).
        Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
        int lift = Math.Max(LogicalToDeviceUnits(48), area.Height / 10);
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - lift);
    }

    private void ApplyTheme()
    {
        bool light = IsLightTheme();
        BackColor = light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(39, 39, 39);
        Color text = light ? Color.FromArgb(26, 26, 26) : Color.White;
        _textLabel.ForeColor = text;
        _iconLabel.ForeColor = light ? Color.FromArgb(0, 95, 184) : Color.FromArgb(96, 205, 255);
    }

    private static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }

    private static FontFamily GetIconFontFamily()
    {
        // Windows 11 ships "Segoe Fluent Icons"; Windows 10 has "Segoe MDL2 Assets".
        foreach (string name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            if (FontFamily.Families.Any(f => f.Name == name))
                return new FontFamily(name);
        }

        return FontFamily.GenericSansSerif;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _holdTimer.Dispose();
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
