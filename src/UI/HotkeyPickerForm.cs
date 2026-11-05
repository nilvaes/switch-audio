namespace SwitchAudio.UI;

public class HotkeyPickerForm : Form
{
    private readonly Label _instructionLabel;
    private readonly Label _previewLabel;

    public uint Modifiers { get; private set; }
    public Keys Key { get; private set; }
    public bool Confirmed { get; private set; }

    public HotkeyPickerForm(uint currentModifiers, Keys currentKey)
    {
        Modifiers = currentModifiers;
        Key = currentKey;

        Text = "Choose hotkey";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 140);
        KeyPreview = true;

        _instructionLabel = new Label
        {
            Text = "Press the key combination you want to use.",
            AutoSize = false,
            Bounds = new Rectangle(16, 16, 328, 40)
        };

        _previewLabel = new Label
        {
            AutoSize = false,
            Bounds = new Rectangle(16, 64, 328, 24),
            Font = new Font(Font.FontFamily, 11, FontStyle.Bold)
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(264, 100, 80, 28)
        };

        Controls.AddRange([_instructionLabel, _previewLabel, cancelButton]);
        CancelButton = cancelButton;

        UpdatePreview();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return;

        // Shift alone isn't allowed: a global Shift+letter hotkey would stop that
        // capital letter from being typed anywhere in Windows.
        if (!e.Control && !e.Alt)
        {
            _instructionLabel.Text = "Include Ctrl or Alt (Shift alone would block normal typing).";
            return;
        }

        uint mods = 0;
        if (e.Control) mods |= Input.HotkeyManager.ModControl;
        if (e.Alt) mods |= Input.HotkeyManager.ModAlt;
        if (e.Shift) mods |= Input.HotkeyManager.ModShift;

        Modifiers = mods;
        Key = e.KeyCode;
        Confirmed = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdatePreview()
    {
        _previewLabel.Text = Input.HotkeyManager.FormatHotkey(Modifiers, Key);
    }
}
