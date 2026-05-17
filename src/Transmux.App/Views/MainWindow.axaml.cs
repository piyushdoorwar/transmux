using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Transmux.App.ViewModels;

namespace Transmux.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
        {
            _vm = vm;
            vm.StorageProvider = StorageProvider;
            vm.ShowAboutDialog = async () =>
            {
                var dialog = new AboutDialog();
                await dialog.ShowDialog(this);
            };
            vm.ShowHistoryDialog = async () =>
            {
                var historyService = new Transmux.App.Services.HistoryService();
                var historyVm = new Transmux.App.ViewModels.HistoryViewModel(historyService);
                var historyWindow = new HistoryWindow { DataContext = historyVm };
                await historyWindow.ShowDialog(this);
            };
            vm.ShowKeyboardShortcutsDialog = async () =>
            {
                var shortcutsDialog = new KeyboardShortcutsDialog();
                await shortcutsDialog.ShowDialog(this);
            };
            vm.ShowSkipConversionDialog = async () =>
            {
                var skipDialog = new SkipConversionDialog();
                await skipDialog.ShowDialog(this);
                return skipDialog.SkipConversion;
            };
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Wire drag-and-drop on the drop zone
        var dropZone = this.FindControl<Border>("DropZone");
        if (dropZone is not null)
        {
            dropZone.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            dropZone.AddHandler(DragDrop.DropEvent, OnDrop);
        }

        // Start animation loop for converting state indicators
        StartProgressAnimations();
    }

    private void StartProgressAnimations()
    {
        var activityPulse = this.FindControl<Ellipse>("ActivityPulse");
        if (activityPulse is null) return;

        // Start continuous pulsing animation on background thread
        _ = Task.Run(async () =>
        {
            while (true)
            {
                // Pulse up
                for (int i = 0; i <= 10; i++)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        activityPulse.Opacity = 0.3 + (0.7 * i / 10.0);
                    });
                    await Task.Delay(40);
                }

                // Pulse down
                for (int i = 10; i >= 0; i--)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        activityPulse.Opacity = 0.3 + (0.7 * i / 10.0);
                    });
                    await Task.Delay(40);
                }

                await Task.Delay(200);
            }
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_vm is null)
        {
            base.OnKeyDown(e);
            return;
        }

        // Ctrl+O = Open file
        if (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control)
        {
            _vm.SelectInputFileCommand.Execute(null);
            e.Handled = true;
        }
        // Enter = Convert
        else if (e.Key == Key.Return)
        {
            if (_vm.HasMedia)
                _vm.StartConversionCommand.Execute(null);
            e.Handled = true;
        }
        // Spacebar = Toggle Fast/Full mode
        else if (e.Key == Key.Space && _vm.HasMedia && !_vm.IsConverting)
        {
            _vm.IsFastConvert = !_vm.IsFastConvert;
            e.Handled = true;
        }

        if (!e.Handled)
            base.OnKeyDown(e);
    }

    // ── Window chrome ─────────────────────────────────────────────────────────

    private void DragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        UpdateMaximizeIcon();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            UpdateMaximizeIcon();
    }

    private void UpdateMaximizeIcon()
    {
        var icon = this.FindControl<PathIcon>("MaximizeIcon");
        if (icon is null) return;
        icon.Data = WindowState == WindowState.Maximized
            ? (Avalonia.Media.Geometry?)this.FindResource("Icon.Restore")
            : (Avalonia.Media.Geometry?)this.FindResource("Icon.Maximize");
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    // ── Drop zone ─────────────────────────────────────────────────────────────

    private void DropZone_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            _vm?.SelectInputFileCommand.Execute(null);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_vm is null) return;
        var files = e.Data.GetFiles()?.ToList();
        if (files is null || files.Count == 0) return;

        // Load the first file
        var path = files[0].Path.LocalPath;
        if (File.Exists(path))
            await _vm.LoadFileAsync(path);

        e.Handled = true;
    }
}
