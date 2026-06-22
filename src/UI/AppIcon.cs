using System.Reflection;

namespace SwitchAudio.UI;

internal static class AppIcon
{
    private static Icon? _cached;

    public static Icon GetTrayIcon()
    {
        if (_cached is not null)
            return _cached;

        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("SwitchAudio.AppIcon.ico");

        _cached = stream is not null
            ? new Icon(stream)
            : SystemIcons.Application;

        return _cached;
    }
}
