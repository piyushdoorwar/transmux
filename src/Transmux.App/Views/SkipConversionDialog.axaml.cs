using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Transmux.App.Views;

public partial class SkipConversionDialog : Window
{
    private bool _skipConversion;

    public SkipConversionDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public bool SkipConversion => _skipConversion;

    private void SkipButton_Click(object? sender, RoutedEventArgs e)
    {
        _skipConversion = true;
        Close();
    }

    private void ConvertButton_Click(object? sender, RoutedEventArgs e)
    {
        _skipConversion = false;
        Close();
    }
}
