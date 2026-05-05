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
        if (files is [var file, ..])
        {
            var path = file.Path.LocalPath;
            if (File.Exists(path))
                await _vm.LoadFileAsync(path);
        }
    }
}
