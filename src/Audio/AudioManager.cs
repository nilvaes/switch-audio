using System.Runtime.InteropServices;

namespace SwitchAudio.Audio;

public record AudioDevice(string Id, string Name);

public class AudioManager
{
    private const uint DeviceStateActive = 0x00000001;

    private static readonly PropertyKey PkeyFriendlyName =
        new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

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
        collection.GetCount(out int count);

        for (int i = 0; i < count; i++)
        {
            collection.Item(i, out var device);
            device.GetId(out string id);
            results.Add(new AudioDevice(id, GetDeviceFriendlyName(device)));
        }

        return results;
    }

    public string GetDefaultDeviceId()
    {
        _enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eConsole, out var device);
        device.GetId(out string id);
        return id;
    }

    public AudioDevice? GetDefaultDevice()
    {
        string id = GetDefaultDeviceId();
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

    public AudioDevice Toggle(string deviceAId, string deviceBId)
    {
        string currentId = GetDefaultDeviceId();
        string targetId = currentId == deviceAId ? deviceBId : deviceAId;
        SetDefaultDevice(targetId);

        return GetPlaybackDevices().FirstOrDefault(d => d.Id == targetId)
            ?? new AudioDevice(targetId, "Unknown device");
    }

    private static string GetDeviceFriendlyName(IMMDevice device)
    {
        try
        {
            device.OpenPropertyStore(0, out var store);
            var key = PkeyFriendlyName;
            store.GetValue(ref key, out var value);
            return value.StringValue ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
