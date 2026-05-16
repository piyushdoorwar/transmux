using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Transmux.Core.Services;
using Transmux.App.ViewModels;
using Transmux.App.Views;

namespace Transmux.App;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var ffmpeg   = new FfmpegService();
            var inspector = new MediaInspector();
            var settings = new SettingsService();
            var vm = new MainViewModel(ffmpeg, inspector, settings);

            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Optional: pre-load a file passed on the command line
            var filePath = TryGetStartupFilePath(desktop.Args);
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                desktop.MainWindow.Opened += async (_, _) =>
                    await vm.LoadFileAsync(filePath);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string? TryGetStartupFilePath(string[]? args)
    {
        if (args is null) return null;

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
                continue;

            var path = arg;
            if (Uri.TryCreate(arg, UriKind.Absolute, out var uri) && uri.IsFile)
                path = uri.LocalPath;

            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
