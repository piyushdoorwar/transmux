using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Transmux.Core.Services;
using Transmux.App.ViewModels;
using Transmux.App.Views;
using Transmux.App.Services;

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
            var history = new HistoryService();
            var vm = new MainViewModel(ffmpeg, inspector, settings, history);

            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Handle files passed via command line (from right-click context menu or file associations)
            var files = TryGetStartupFiles(desktop.Args);
            if (files.Count > 0)
            {
                desktop.MainWindow.Opened += (_, _) =>
                {
                    if (files.Count == 1)
                    {
                        // Single file: load as current file
                        _ = vm.LoadFileAsync(files[0]);
                    }
                    else
                    {
                        // Multiple files: add to batch queue
                        foreach (var file in files)
                            vm.AddFileToBatchQueue(file);
                    }
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static List<string> TryGetStartupFiles(string[]? args)
    {
        var files = new List<string>();
        if (args is null) return files;

        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
                continue;

            var path = arg;
            if (Uri.TryCreate(arg, UriKind.Absolute, out var uri) && uri.IsFile)
                path = uri.LocalPath;

            if (File.Exists(path))
                files.Add(path);
        }

        return files;
    }
}
