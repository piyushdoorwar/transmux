using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Transmux.App.Views;

public partial class AboutDialog : Window
{
    private const string GitHubUrl = "https://github.com/piyushdoorwar/transmux";
    private const string ReleasesUrl = "https://github.com/piyushdoorwar/transmux/releases";

    public AboutDialog()
    {
        AvaloniaXamlLoader.Load(this);

        SetField("VersionText", $"Version {GetAppVersion()}");
        SetField("OsText", GetOsName());
        SetField("ArchText", RuntimeInformation.OSArchitecture.ToString());
        SetField("RuntimeText", $".NET {Environment.Version}");
    }

    private void SetField(string name, string text)
    {
        if (this.FindControl<TextBlock>(name) is { } tb) tb.Text = text;
    }

    private static string GetAppVersion()
    {
        var informational = typeof(AboutDialog).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+')[0];

        return typeof(AboutDialog).Assembly.GetName().Version?.ToString(3) ?? "0.0.0-dev";
    }

    private static string GetOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return $"Windows {Environment.OSVersion.Version.Major}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return $"macOS {Environment.OSVersion.Version.Major}";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var lines = File.ReadAllLines("/etc/os-release");
                var prettyLine = lines.FirstOrDefault(l => l.StartsWith("PRETTY_NAME="));
                if (prettyLine is not null)
                    return prettyLine.Split('=', 2)[1].Trim('"');
            }
            catch { }
            return "Linux";
        }
        return RuntimeInformation.OSDescription;
    }

    private void GitHub_Click(object? sender, RoutedEventArgs e) => OpenUrl(GitHubUrl);
    private void Releases_Click(object? sender, RoutedEventArgs e) => OpenUrl(ReleasesUrl);

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
