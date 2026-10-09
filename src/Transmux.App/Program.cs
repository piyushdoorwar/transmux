using Avalonia;
using Avalonia.Media;
using Avalonia.X11;

namespace Transmux.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SESSION_MANAGER")))
            Environment.SetEnvironmentVariable("SESSION_MANAGER", "");

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // DM Sans (embedded) is the default UI font for every window, popup and tooltip.
            .With(new FontManagerOptions
            {
                DefaultFamilyName = "avares://Transmux/Assets/Fonts#DM Sans",
            })
            .LogToTrace()
            // Disable IBus IME — Ubuntu 26.04 IBus dropped several methods that
            // Avalonia still calls, causing cascading DBus errors in every dialog.
            .With(new X11PlatformOptions { EnableIme = false });
    }
}
