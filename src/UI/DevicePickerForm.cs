using SwitchAudio.Audio;

namespace SwitchAudio.UI;

public class DevicePickerForm : Form
{
    private readonly ComboBox _deviceA;
    private readonly ComboBox _deviceB;
    private readonly Label _errorLabel;

    public string? SelectedDeviceAId { get; private set; }
    public string? SelectedDeviceAName { get; private set; }
    public string? SelectedDeviceBId { get; private set; }
    public string? SelectedDeviceBName { get; private set; }

    public DevicePickerForm(
        IReadOnlyList<AudioDevice> devices,
        string? currentAId,
        string? currentBId)
    {
        // Layout below is in 96-DPI pixels; scale it to the screen's DPI.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;

        Text = "Choose devices";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 220);

        var labelA = new Label
        {
            Text = "Device A:",
            AutoSize = true,
            Location = new Point(16, 20)
        };

        _deviceA = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(16, 44),
            Width = 388,
            DisplayMember = nameof(AudioDevice.Name)
        };

        var labelB = new Label
        {
            Text = "Device B:",
            AutoSize = true,
            Location = new Point(16, 84)
        };

        _deviceB = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(16, 108),
            Width = 388,
            DisplayMember = nameof(AudioDevice.Name)
        };

        _errorLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 144),
            Size = new Size(280, 32),
            ForeColor = Color.DarkRed,
            Visible = false
        };

        foreach (var device in devices)
        {
            _deviceA.Items.Add(device);
            _deviceB.Items.Add(device);
        }

        SelectDevice(_deviceA, devices, currentAId, fallbackIndex: 0);
        SelectDevice(_deviceB, devices, currentBId, fallbackIndex: Math.Min(1, devices.Count - 1));

        var saveButton = new Button
        {
            Text = "Save",
            Location = new Point(244, 176),
            Width = 80,
            DialogResult = DialogResult.None
        };
        saveButton.Click += (_, _) => TrySave();

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(324, 176),
            Width = 80,
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.AddRange([
            labelA, _deviceA, labelB, _deviceB, _errorLabel, saveButton, cancelButton
        ]);
    }

    private static void SelectDevice(
        ComboBox combo,
        IReadOnlyList<AudioDevice> devices,
        string? deviceId,
        int fallbackIndex)
    {
        int index = string.IsNullOrEmpty(deviceId)
            ? fallbackIndex
            : devices.ToList().FindIndex(d => d.Id == deviceId);

        combo.SelectedIndex = index >= 0 ? index : fallbackIndex;
    }

    private void TrySave()
    {
        if (_deviceA.SelectedItem is not AudioDevice deviceA ||
            _deviceB.SelectedItem is not AudioDevice deviceB)
        {
            ShowError("Pick both devices.");
            return;
        }

        if (deviceA.Id == deviceB.Id)
        {
            ShowError("Device A and Device B must be different.");
            return;
        }

        SelectedDeviceAId = deviceA.Id;
        SelectedDeviceAName = deviceA.Name;
        SelectedDeviceBId = deviceB.Id;
        SelectedDeviceBName = deviceB.Name;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ShowError(string message)
    {
        _errorLabel.Text = message;
        _errorLabel.Visible = true;
    }
}
