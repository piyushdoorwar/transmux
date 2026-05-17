using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Transmux.App.Models;

public sealed class BatchConversionJob : INotifyPropertyChanged
{
    private string _status = "Pending";
    private double _progressPercent;

    public BatchConversionJob(string inputPath)
    {
        InputPath = inputPath;
        FileName = Path.GetFileName(inputPath);
    }

    public string InputPath { get; }
    public string FileName { get; }

    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            OnPropertyChanged();
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set
        {
            if (_progressPercent == value) return;
            _progressPercent = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
