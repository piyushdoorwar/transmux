using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

        // If dropping multiple files or Ctrl/Shift is held, add to batch queue
        if (files.Count > 1 || (e.KeyModifiers & KeyModifiers.Control) != 0 ||
            (e.KeyModifiers & KeyModifiers.Shift) != 0)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (File.Exists(path))
                    _vm.AddFileToBatchQueue(path);
            }
        }
        else
        {
            // Single file without modifiers: load as current file
            var path = files[0].Path.LocalPath;
            if (File.Exists(path))
                await _vm.LoadFileAsync(path);
        }

        e.Handled = true;
    }
}
