using System.ComponentModel;
using System.Windows.Input;
using Transmux.App.Models;
using Transmux.App.Services;

namespace Transmux.App.ViewModels;

public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private readonly HistoryService _historyService;
    private List<ConversionRecord> _displayItems;

    public HistoryViewModel(HistoryService historyService)
    {
        _historyService = historyService;
        _displayItems = _historyService.History.ToList();

        ClearHistoryCommand = new RelayCommand(_ => { ClearHistory(); return Task.CompletedTask; }, _ => _displayItems.Count > 0);
        OpenFolderCommand = new RelayCommand(p => { OpenOutputFolder(p as ConversionRecord); return Task.CompletedTask; });
    }

    public List<ConversionRecord> DisplayItems
    {
        get => _displayItems;
        set
        {
            if (_displayItems == value) return;
            _displayItems = value;
            OnPropertyChanged();
        }
    }

    public ICommand ClearHistoryCommand { get; }
    public ICommand OpenFolderCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void ClearHistory()
    {
        _historyService.ClearHistory();
        DisplayItems = [];
        ((RelayCommand)ClearHistoryCommand).RaiseCanExecuteChanged();
    }

    public void OpenOutputFolder(ConversionRecord? record)
    {
        if (record is null) return;

        try
        {
            var folderPath = Path.GetDirectoryName(record.OutputPath);
            if (folderPath is not null && Directory.Exists(folderPath))
            {
#if _WINDOWS
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = folderPath,
                    UseShellExecute = true
                });
#elif __LINUX__
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = folderPath,
                    UseShellExecute = false
                });
#elif __MACOS__
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = folderPath,
                    UseShellExecute = false
                });
#endif
            }
        }
        catch
        {
            // Silently fail if unable to open folder
        }
    }
}
