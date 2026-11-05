using System.Runtime.InteropServices;

namespace SwitchAudio.Audio;

public record AudioDevice(string Id, string Name);

public class AudioManager
{
    private const uint DeviceStateActive = 0x00000001;

    private static readonly PropertyKey PkeyFriendlyName =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    private readonly IMMDeviceEnumerator _enumerator;
    private readonly IPolicyConfig _policyConfig;

    public AudioManager()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
        _policyConfig = (IPolicyConfig)new PolicyConfigCom();
    }

    public List<AudioDevice> GetPlaybackDevices()
    {
        var results = new List<AudioDevice>();
        _enumerator.EnumAudioEndpoints(EDataFlow.eRender, DeviceStateActive, out var collection);
        try
        {
            collection.GetCount(out int count);

            for (int i = 0; i < count; i++)
            {
                collection.Item(i, out var device);
                try
                {
                    device.GetId(out string id);
                    results.Add(new AudioDevice(id, GetDeviceFriendlyName(device)));
                }
                finally
                {
                    Release(device);
                }
            }
        }
        finally
        {
            Release(collection);
        }

        return results;
    }

    // Returns null when Windows has no default output (e.g. every device is unplugged or disabled).
    public string? GetDefaultDeviceId()
    {
        try
        {
            int hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out var device);
            if (hr != 0 || device is null)
                return null;

            try
            {
                device.GetId(out string id);
                return id;
            }
            finally
            {
                Release(device);
            }
        }
        catch (COMException)
        {
            return null;
        }
    }

    public AudioDevice? GetDefaultDevice()
    {
        string? id = GetDefaultDeviceId();
        if (id is null)
            return null;

        return GetPlaybackDevices().FirstOrDefault(d => d.Id == id);
    }

    public void SetDefaultDevice(string deviceId)
    {
        ThrowIfFailed(_policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole), "set default console device");
        ThrowIfFailed(_policyConfig.SetDefaultEndpoint(deviceId, ERole.eMultimedia), "set default multimedia device");
        ThrowIfFailed(_policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications), "set default communications device");
    }

    private static void ThrowIfFailed(int hresult, string action)
    {
        if (hresult != 0)
            throw new InvalidOperationException($"Failed to {action} (error 0x{hresult:X8}).");
    }

    private static string GetDeviceFriendlyName(IMMDevice device)
    {
        try
        {
            device.OpenPropertyStore(0, out var store);
            try
            {
                var key = PkeyFriendlyName;
                store.GetValue(ref key, out var value);
                try
                {
                    return value.StringValue ?? "Unknown";
                }
                finally
                {
                    // The name string is allocated by Windows and must be freed.
                    PropVariantClear(ref value);
                }
            }
            finally
            {
                Release(store);
            }
        }
        catch
        {
            return "Unknown";
        }
    }

    // Windows audio objects (and the registry keys a property store holds open) are otherwise
    // only freed when .NET's garbage collector happens to run — rarely, in an idle tray app —
    // so handles piled up with every switch. Release each one as soon as we're done with it.
    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
            Marshal.ReleaseComObject(comObject);
    }
}
