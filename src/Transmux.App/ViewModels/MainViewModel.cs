using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Transmux.Core.Models;
using Transmux.Core.Services;

namespace Transmux.App.ViewModels;

public sealed record SubtitleModeOption(SubtitleMode Mode, string Label);

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly FfmpegService _ffmpeg;
    private readonly MediaInspector _inspector;
    private readonly SettingsService _settings;

    // ── State ────────────────────────────────────────────────────────────────

    private string? _inputFilePath;
    private MediaInfo? _detectedMedia;
    private FormatInfo? _selectedFormat;
    private SubtitleMode _selectedSubtitleMode = SubtitleMode.None;
    private string? _outputFilePath;

    // Conversion mode
    private bool _isFastConvert;

    // Conversion
    private bool _isInspecting;
    private bool _isConverting;
    private bool _isComplete;
    private double _progressPercent;
    private string _speedText = "";
    private string _etaText = "";
    private string? _errorMessage;
    private CancellationTokenSource? _cts;

    public MainViewModel(FfmpegService ffmpeg, MediaInspector inspector, SettingsService settings)
    {
        _ffmpeg = ffmpeg;
        _inspector = inspector;
        _settings = settings;

        Formats = OutputFormats.All;

        // Restore last format
        var lastId = settings.LastOutputFormatId;
        _selectedFormat = lastId is not null
            ? OutputFormats.All.FirstOrDefault(f => f.Id == lastId) ?? OutputFormats.Mp4H264
            : OutputFormats.Mp4H264;

        SubtitleModes =
        [
            new SubtitleModeOption(SubtitleMode.Include, "Include in output"),
            new SubtitleModeOption(SubtitleMode.ExtractSrt, "Extract to .srt file"),
            new SubtitleModeOption(SubtitleMode.ExtractAss, "Extract to .ass file"),
            new SubtitleModeOption(SubtitleMode.None, "Strip subtitles"),
        ];
        _selectedSubtitleModeOption = SubtitleModes[0];
        _selectedSubtitleMode = SubtitleMode.Include;

        SelectInputFileCommand = new RelayCommand(async _ => await SelectInputFileAsync());
        BrowseOutputPathCommand = new RelayCommand(async _ => await BrowseOutputPathAsync(), _ => HasMedia);
        StartConversionCommand = new RelayCommand(async _ => await StartConversionAsync(), _ => CanConvert);
        CancelConversionCommand = new RelayCommand(_ => CancelConversion(), _ => _isConverting);
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => _isComplete && _outputFilePath is not null);
        OpenAboutDialogCommand = new RelayCommand(async _ => await OpenAboutDialogAsync());
        SetFastConvertOnCommand = new RelayCommand(_ => IsFastConvert = true, _ => IsInputEnabled);
        SetFullConvertCommand = new RelayCommand(_ => IsFastConvert = false, _ => IsInputEnabled);
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public IReadOnlyList<FormatInfo> Formats { get; }
    public IReadOnlyList<SubtitleModeOption> SubtitleModes { get; }

    private SubtitleModeOption? _selectedSubtitleModeOption;

    public SubtitleModeOption? SelectedSubtitleModeOption
    {
        get => _selectedSubtitleModeOption;
        set { SetField(ref _selectedSubtitleModeOption, value); SelectedSubtitleMode = value?.Mode ?? SubtitleMode.None; }
    }

    public string? InputFilePath
    {
        get => _inputFilePath;
        private set { SetField(ref _inputFilePath, value); OnPropertyChanged(nameof(InputFileName)); OnPropertyChanged(nameof(HasMedia)); }
    }

    public string? InputFileName => _inputFilePath is null ? null : Path.GetFileName(_inputFilePath);

    public MediaInfo? DetectedMedia
    {
        get => _detectedMedia;
        private set
        {
            SetField(ref _detectedMedia, value);
            OnPropertyChanged(nameof(HasMedia));
            OnPropertyChanged(nameof(MediaSummaryFormat));
            OnPropertyChanged(nameof(MediaSummaryVideo));
            OnPropertyChanged(nameof(MediaSummaryAudio));
            OnPropertyChanged(nameof(MediaSummarySubtitles));
            OnPropertyChanged(nameof(ShowSubtitleMode));
        }
    }

    public bool HasMedia => _detectedMedia is not null;

    public string MediaSummaryFormat =>
        _detectedMedia is null ? "" :
        $"{_detectedMedia.FormatLongName}  ·  {FormatDuration(_detectedMedia.Duration)}";

    public string MediaSummaryVideo
    {
        get
        {
            var v = _detectedMedia?.VideoStream;
            if (v is null) return "No video stream";
            var res = v.Width > 0 ? $"  ·  {v.Width}×{v.Height}" : "";
            var fps = v.FrameRate is not null ? $"  ·  {v.FrameRate} fps" : "";
            return $"{v.CodecName.ToUpperInvariant()}{res}{fps}";
        }
    }

    public string MediaSummaryAudio
    {
        get
        {
            var streams = _detectedMedia?.AudioStreams.ToList();
            if (streams is null || streams.Count == 0) return "No audio stream";
            var first = streams[0];
            var ch = first.Channels switch { 1 => "mono", 2 => "stereo", var n => $"{n}ch" };
            var sr = first.SampleRate > 0 ? $"  ·  {first.SampleRate / 1000.0:G3} kHz" : "";
            var extra = streams.Count > 1 ? $"  (+{streams.Count - 1} more)" : "";
            return $"{first.CodecName.ToUpperInvariant()}  ·  {ch}{sr}{extra}";
        }
    }

    public string MediaSummarySubtitles
    {
        get
        {
            var count = _detectedMedia?.SubtitleTrackCount ?? 0;
            return count == 0 ? "None" : $"{count} track{(count == 1 ? "" : "s")}";
        }
    }

    public bool ShowSubtitleMode => _detectedMedia?.HasSubtitles == true;

    public bool IsFastConvert
    {
        get => _isFastConvert;
        set
        {
            if (SetField(ref _isFastConvert, value))
                OnPropertyChanged(nameof(IsFullConvert));
        }
    }

    public bool IsFullConvert => !_isFastConvert;

    public FormatInfo? SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetField(ref _selectedFormat, value) && value is not null)
            {
                _settings.LastOutputFormatId = value.Id;
                // Update output path extension if a path is already set
                if (_outputFilePath is not null)
                    OutputFilePath = Path.ChangeExtension(_outputFilePath, value.Extension);
                ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public SubtitleMode SelectedSubtitleMode
    {
        get => _selectedSubtitleMode;
        set => SetField(ref _selectedSubtitleMode, value);
    }

    public string? OutputFilePath
    {
        get => _outputFilePath;
        set
        {
            SetField(ref _outputFilePath, value);
            ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsInspecting
    {
        get => _isInspecting;
        private set
        {
            SetField(ref _isInspecting, value);
            OnPropertyChanged(nameof(IsInputEnabled));
            ((RelayCommand)SetFastConvertOnCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SetFullConvertCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsInputEnabled => !_isInspecting && !_isConverting;

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            SetField(ref _isConverting, value);
            OnPropertyChanged(nameof(IsInputEnabled));
            ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CancelConversionCommand).RaiseCanExecuteChanged();
            ((RelayCommand)BrowseOutputPathCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SetFastConvertOnCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SetFullConvertCommand).RaiseCanExecuteChanged();
        }
    }

    public bool IsComplete
    {
        get => _isComplete;
        private set
        {
            SetField(ref _isComplete, value);
            ((RelayCommand)OpenOutputFolderCommand).RaiseCanExecuteChanged();
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetField(ref _speedText, value);
    }

    public string EtaText
    {
        get => _etaText;
        private set => SetField(ref _etaText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetField(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => _errorMessage is not null;

    public bool CanConvert =>
        HasMedia &&
        _selectedFormat is not null &&
        !string.IsNullOrWhiteSpace(_outputFilePath) &&
        !_isConverting &&
        !_isInspecting;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SelectInputFileCommand { get; }
    public ICommand BrowseOutputPathCommand { get; }
    public ICommand StartConversionCommand { get; }
    public ICommand CancelConversionCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand OpenAboutDialogCommand { get; }
    public ICommand SetFastConvertOnCommand { get; }
    public ICommand SetFullConvertCommand { get; }

    // Injected by the Window after construction
    public IStorageProvider? StorageProvider { get; set; }
    public Func<Task>? ShowAboutDialog { get; set; }

    // ── Actions ───────────────────────────────────────────────────────────────

    public async Task LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        IsInspecting = true;
        IsComplete = false;
        ErrorMessage = null;
        DetectedMedia = null;
        InputFilePath = filePath;

        try
        {
            var info = await _inspector.InspectAsync(filePath);
            DetectedMedia = info;

            // Default output path: same dir as input, same name, new extension
            var dir = _settings.LastOutputDirectory ?? Path.GetDirectoryName(filePath) ?? "";
            var name = Path.GetFileNameWithoutExtension(filePath);
            var ext = _selectedFormat?.Extension ?? ".mp4";
            OutputFilePath = Path.Combine(dir, name + ext);

            // Default subtitle mode
            var targetMode = info.HasSubtitles ? SubtitleMode.Include : SubtitleMode.None;
            SelectedSubtitleModeOption = SubtitleModes.FirstOrDefault(m => m.Mode == targetMode) ?? SubtitleModes[0];

            // If input is audio-only, default to MP3
            if (!info.HasVideo && _selectedFormat?.IsAudioOnly == false)
                SelectedFormat = OutputFormats.Mp3;

            ((RelayCommand)BrowseOutputPathCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not read file: {ex.Message}";
            InputFilePath = null;
        }
        finally
        {
            IsInspecting = false;
        }
    }

    private async Task SelectInputFileAsync()
    {
        if (StorageProvider is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open media file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Audio / Video")
                {
                    Patterns = ["*.mp4", "*.mkv", "*.avi", "*.mov", "*.webm", "*.flv", "*.ts",
                                "*.m2ts", "*.wmv", "*.mpg", "*.mpeg", "*.3gp",
                                "*.mp3", "*.aac", "*.flac", "*.ogg", "*.wav", "*.opus",
                                "*.m4a", "*.m4b", "*.wma", "*.aiff", "*.alac"]
                },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        if (files is [var file, ..])
            await LoadFileAsync(file.Path.LocalPath);
    }

    private async Task BrowseOutputPathAsync()
    {
        if (StorageProvider is null || _selectedFormat is null) return;

        var suggested = _outputFilePath is not null
            ? Path.GetFileName(_outputFilePath)
            : (Path.GetFileNameWithoutExtension(_inputFilePath) + _selectedFormat.Extension);

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save output file",
            SuggestedFileName = suggested,
            DefaultExtension = _selectedFormat.Extension.TrimStart('.'),
            FileTypeChoices =
            [
                new FilePickerFileType(_selectedFormat.DisplayName)
                {
                    Patterns = [$"*{_selectedFormat.Extension}"]
                }
            ]
        });

        if (file is not null)
        {
            var path = file.Path.LocalPath;
            _settings.LastOutputDirectory = Path.GetDirectoryName(path);
            OutputFilePath = path;
        }
    }

    private async Task StartConversionAsync()
    {
        if (_inputFilePath is null || _outputFilePath is null ||
            _selectedFormat is null || _detectedMedia is null)
            return;

        IsConverting = true;
        IsComplete = false;
        ErrorMessage = null;
        ProgressPercent = 0;
        SpeedText = "";
        EtaText = "";

        _cts = new CancellationTokenSource();

        string? subOutputPath = null;
        if (_selectedSubtitleMode == SubtitleMode.ExtractSrt)
            subOutputPath = Path.ChangeExtension(_outputFilePath, ".srt");
        else if (_selectedSubtitleMode == SubtitleMode.ExtractAss)
            subOutputPath = Path.ChangeExtension(_outputFilePath, ".ass");

        var options = new ConversionOptions(
            InputPath: _inputFilePath,
            OutputPath: _outputFilePath,
            SubtitleOutputPath: subOutputPath,
            Format: _selectedFormat,
            SubtitleMode: _selectedSubtitleMode,
            InputDuration: _detectedMedia.Duration,
            FastConvert: _isFastConvert);

        var progress = new Progress<ConversionProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = p.Percent;
                SpeedText = p.Speed > 0 ? $"{p.Speed:F1}×" : "";
                EtaText = p.Eta.HasValue
                    ? $"~{(int)p.Eta.Value.TotalMinutes:D2}:{p.Eta.Value.Seconds:D2}"
                    : "";
            });
        });

        try
        {
            await _ffmpeg.ConvertAsync(options, progress, _cts.Token);
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = 100;
                IsConverting = false;
                IsComplete = true;
            });
        }
        catch (OperationCanceledException)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsConverting = false;
                ProgressPercent = 0;
                SpeedText = "";
                EtaText = "";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsConverting = false;
                ErrorMessage = ex.Message;
            });
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelConversion()
    {
        _cts?.Cancel();
    }

    private void OpenOutputFolder()
    {
        if (_outputFilePath is null) return;
        var dir = Path.GetDirectoryName(_outputFilePath);
        if (dir is null) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = dir,
                UseShellExecute = true
            });
        }
        catch { /* best-effort */ }
    }

    private async Task OpenAboutDialogAsync()
    {
        if (ShowAboutDialog is not null)
            await ShowAboutDialog();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatDuration(TimeSpan t)
    {
        if (t == TimeSpan.Zero) return "Unknown";
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    // ── RelayCommand ──────────────────────────────────────────────────────────

    private sealed class RelayCommand : ICommand
    {
        private readonly Func<object?, Task> _executeAsync;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
            : this(_ => { execute(_); return Task.CompletedTask; }, canExecute) { }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public async void Execute(object? parameter)
        {
            try { await _executeAsync(parameter); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RelayCommand] {ex.Message}");
            }
        }

        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
