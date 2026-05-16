using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Transmux.Core.Models;
using Transmux.Core.Services;

namespace Transmux.App.ViewModels;

public sealed record SubtitleModeOption(SubtitleMode Mode, string Label);
public enum MediaJobMode { Convert, GenerateSubtitles }
public sealed record JobModeOption(MediaJobMode Mode, string Label);

public sealed class SubtitleTrackOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public SubtitleTrackOption(int subtitleIndex, StreamInfo stream, string label, bool isSelected)
    {
        SubtitleIndex = subtitleIndex;
        StreamIndex = stream.Index;
        Language = stream.Language;
        Title = stream.Title;
        CodecName = stream.CodecName;
        Label = label;
        _isSelected = isSelected;
    }

    public int SubtitleIndex { get; }
    public int StreamIndex { get; }
    public string? Language { get; }
    public string? Title { get; }
    public string CodecName { get; }
    public string Label { get; }
    public event EventHandler? SelectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly FfmpegService _ffmpeg;
    private readonly WhisperSubtitleService _whisper;
    private readonly MediaInspector _inspector;
    private readonly SettingsService _settings;

    // ── State ────────────────────────────────────────────────────────────────

    private string? _inputFilePath;
    private MediaInfo? _detectedMedia;
    private FormatInfo? _selectedFormat;
    private MediaJobMode _selectedJobMode = MediaJobMode.Convert;
    private SubtitleMode _selectedSubtitleMode = SubtitleMode.None;
    private WhisperModelInfo _selectedWhisperModel = Transmux.Core.Models.WhisperModels.Fast;
    private string _whisperLanguage = "auto";
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
    private string _jobStatusText = "";
    private string? _errorMessage;
    private CancellationTokenSource? _cts;

    public MainViewModel(
        FfmpegService ffmpeg,
        WhisperSubtitleService whisper,
        MediaInspector inspector,
        SettingsService settings)
    {
        _ffmpeg = ffmpeg;
        _whisper = whisper;
        _inspector = inspector;
        _settings = settings;

        Formats = OutputFormats.All;
        WhisperModels = Transmux.Core.Models.WhisperModels.All;
        JobModes =
        [
            new JobModeOption(MediaJobMode.Convert, "Video conversion"),
            new JobModeOption(MediaJobMode.GenerateSubtitles, "Subs generation"),
        ];
        _selectedJobModeOption = JobModes[0];

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
        SetConvertModeCommand = new RelayCommand(_ => SelectedJobMode = MediaJobMode.Convert, _ => IsInputEnabled);
        SetSubtitlesModeCommand = new RelayCommand(_ => SelectedJobMode = MediaJobMode.GenerateSubtitles, _ => IsInputEnabled);
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public IReadOnlyList<FormatInfo> Formats { get; }
    public IReadOnlyList<SubtitleModeOption> SubtitleModes { get; }
    public IReadOnlyList<WhisperModelInfo> WhisperModels { get; }
    public IReadOnlyList<JobModeOption> JobModes { get; }
    public List<SubtitleTrackOption> SubtitleTracks { get; } = [];

    private JobModeOption? _selectedJobModeOption;
    private SubtitleModeOption? _selectedSubtitleModeOption;

    public JobModeOption? SelectedJobModeOption
    {
        get => _selectedJobModeOption;
        set
        {
            SetField(ref _selectedJobModeOption, value);
            SelectedJobMode = value?.Mode ?? MediaJobMode.Convert;
        }
    }

    public SubtitleModeOption? SelectedSubtitleModeOption
    {
        get => _selectedSubtitleModeOption;
        set
        {
            SetField(ref _selectedSubtitleModeOption, value);
            SelectedSubtitleMode = value?.Mode ?? SubtitleMode.None;
        }
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
            OnPropertyChanged(nameof(ShowSubtitleTrackSelector));
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

    public bool ShowSubtitleMode => IsConversionMode && _detectedMedia?.HasSubtitles == true;
    public bool IsConversionMode => _selectedJobMode == MediaJobMode.Convert;
    public bool IsSubtitleGenerationMode => _selectedJobMode == MediaJobMode.GenerateSubtitles;

    public bool ShowSubtitleTrackSelector =>
        _detectedMedia?.SubtitleTrackCount > 1 &&
        SelectedSubtitleMode is SubtitleMode.ExtractSrt or SubtitleMode.ExtractAss;

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

    public MediaJobMode SelectedJobMode
    {
        get => _selectedJobMode;
        set
        {
            if (!SetField(ref _selectedJobMode, value))
                return;

            OnPropertyChanged(nameof(IsConversionMode));
            OnPropertyChanged(nameof(IsSubtitleGenerationMode));
            OnPropertyChanged(nameof(ShowSubtitleMode));
            OnPropertyChanged(nameof(ShowSubtitleTrackSelector));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(CompletionText));
            UpdateOutputPathExtensionForMode();
            ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
        }
    }

    public FormatInfo? SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetField(ref _selectedFormat, value) && value is not null)
            {
                _settings.LastOutputFormatId = value.Id;
                // Update output path extension if a path is already set
                if (_outputFilePath is not null && IsConversionMode)
                    OutputFilePath = Path.ChangeExtension(_outputFilePath, value.Extension);
                ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public WhisperModelInfo SelectedWhisperModel
    {
        get => _selectedWhisperModel;
        set
        {
            if (SetField(ref _selectedWhisperModel, value))
                ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
        }
    }

    public string WhisperLanguage
    {
        get => _whisperLanguage;
        set => SetField(ref _whisperLanguage, value);
    }

    public SubtitleMode SelectedSubtitleMode
    {
        get => _selectedSubtitleMode;
        set
        {
            if (SetField(ref _selectedSubtitleMode, value))
            {
                OnPropertyChanged(nameof(ShowSubtitleTrackSelector));
                ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
            }
        }
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
            ((RelayCommand)SetConvertModeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SetSubtitlesModeCommand).RaiseCanExecuteChanged();
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
            ((RelayCommand)SetConvertModeCommand).RaiseCanExecuteChanged();
            ((RelayCommand)SetSubtitlesModeCommand).RaiseCanExecuteChanged();
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

    public string JobStatusText
    {
        get => _jobStatusText;
        private set => SetField(ref _jobStatusText, value);
    }

    public string ActionButtonText =>
        IsSubtitleGenerationMode ? "Generate subtitles" : "Convert";

    public string CompletionText =>
        IsSubtitleGenerationMode ? "Subtitle file complete" : "Conversion complete";

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { SetField(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => _errorMessage is not null;

    public bool CanConvert =>
        HasMedia &&
        (IsSubtitleGenerationMode || _selectedFormat is not null) &&
        (!IsSubtitleGenerationMode || _detectedMedia?.HasAudio == true) &&
        !string.IsNullOrWhiteSpace(_outputFilePath) &&
        (IsSubtitleGenerationMode || HasRequiredSubtitleSelection) &&
        !_isConverting &&
        !_isInspecting;

    private bool HasRequiredSubtitleSelection =>
        _selectedSubtitleMode is not (SubtitleMode.ExtractSrt or SubtitleMode.ExtractAss) ||
        SubtitleTracks.Any(t => t.IsSelected);

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand SelectInputFileCommand { get; }
    public ICommand BrowseOutputPathCommand { get; }
    public ICommand StartConversionCommand { get; }
    public ICommand CancelConversionCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand OpenAboutDialogCommand { get; }
    public ICommand SetFastConvertOnCommand { get; }
    public ICommand SetFullConvertCommand { get; }
    public ICommand SetConvertModeCommand { get; }
    public ICommand SetSubtitlesModeCommand { get; }

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
            BuildSubtitleTracks(info);

            // Default output path: same dir as input, same name, new extension
            var dir = _settings.LastOutputDirectory ?? Path.GetDirectoryName(filePath) ?? "";
            var name = Path.GetFileNameWithoutExtension(filePath);
            var ext = IsSubtitleGenerationMode ? ".srt" : _selectedFormat?.Extension ?? ".mp4";
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
        if (StorageProvider is null) return;

        var extension = IsSubtitleGenerationMode ? ".srt" : _selectedFormat?.Extension;
        if (string.IsNullOrWhiteSpace(extension)) return;

        var suggested = _outputFilePath is not null
            ? Path.GetFileName(_outputFilePath)
            : (Path.GetFileNameWithoutExtension(_inputFilePath) + extension);

        var fileTypeName = IsSubtitleGenerationMode
            ? "SubRip subtitles"
            : _selectedFormat?.DisplayName ?? "Output file";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save output file",
            SuggestedFileName = suggested,
            DefaultExtension = extension.TrimStart('.'),
            FileTypeChoices =
            [
                new FilePickerFileType(fileTypeName)
                {
                    Patterns = [$"*{extension}"]
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
        if (_inputFilePath is null || _outputFilePath is null || _detectedMedia is null)
            return;

        IsConverting = true;
        IsComplete = false;
        ErrorMessage = null;
        ProgressPercent = 0;
        SpeedText = "";
        EtaText = "";
        JobStatusText = IsSubtitleGenerationMode ? "Preparing subtitles" : "Converting";

        _cts = new CancellationTokenSource();

        if (IsSubtitleGenerationMode)
        {
            await StartSubtitleGenerationAsync();
            return;
        }

        if (_selectedFormat is null)
            return;

        string? subOutputPath = null;
        if (_selectedSubtitleMode == SubtitleMode.ExtractSrt)
            subOutputPath = BuildSubtitleOutputPath(".srt");
        else if (_selectedSubtitleMode == SubtitleMode.ExtractAss)
            subOutputPath = BuildSubtitleOutputPath(".ass");

        var subtitleTracks = BuildSelectedSubtitleExtractions(subOutputPath).ToList();

        var options = new ConversionOptions(
            InputPath: _inputFilePath,
            OutputPath: _outputFilePath,
            SubtitleOutputPath: subOutputPath,
            SubtitleTracks: subtitleTracks,
            Format: _selectedFormat,
            SubtitleMode: _selectedSubtitleMode,
            InputDuration: _detectedMedia.Duration,
            FastConvert: _isFastConvert);

        var progress = new Progress<ConversionProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = p.Percent;
                JobStatusText = $"Converting · {p.Percent:F0}%";
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

    private async Task StartSubtitleGenerationAsync()
    {
        if (_inputFilePath is null || _outputFilePath is null || _detectedMedia is null || _cts is null)
            return;

        var options = new SubtitleGenerationOptions(
            InputPath: _inputFilePath,
            OutputPath: _outputFilePath,
            Model: _selectedWhisperModel,
            InputDuration: _detectedMedia.Duration,
            Language: NormalizeWhisperLanguage(_whisperLanguage),
            ThreadCount: GetWhisperThreadCount());

        var progress = new Progress<ConversionProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = p.Percent;
                var phase = p.Percent < 35 ? "Extracting audio" : "Generating subtitles";
                JobStatusText = $"{phase} · {p.Percent:F0}%";
                SpeedText = _selectedWhisperModel.DisplayName;
                EtaText = "";
            });
        });

        var setupProgress = new Progress<string>(message =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                JobStatusText = message;
                ProgressPercent = 0;
            });
        });

        try
        {
            await _whisper.GenerateSrtAsync(options, progress, _cts.Token, setupProgress);
            Dispatcher.UIThread.Post(() =>
            {
                ProgressPercent = 100;
                IsConverting = false;
                IsComplete = true;
                JobStatusText = "";
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
                JobStatusText = "";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsConverting = false;
                ErrorMessage = ex.Message;
                JobStatusText = "";
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

    private void UpdateOutputPathExtensionForMode()
    {
        if (_outputFilePath is null)
            return;

        var extension = IsSubtitleGenerationMode
            ? ".srt"
            : _selectedFormat?.Extension;

        if (!string.IsNullOrWhiteSpace(extension))
            OutputFilePath = Path.ChangeExtension(_outputFilePath, extension);
    }

    private static string NormalizeWhisperLanguage(string language)
    {
        var value = language.Trim();
        return value.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "" : value;
    }

    private static int GetWhisperThreadCount()
    {
        var processorCount = Environment.ProcessorCount;
        return Math.Clamp(processorCount / 2, 1, 8);
    }

    private void BuildSubtitleTracks(MediaInfo info)
    {
        foreach (var track in SubtitleTracks)
            track.SelectionChanged -= SubtitleTrack_SelectionChanged;

        SubtitleTracks.Clear();

        var subtitleStreams = info.SubtitleStreams.ToList();
        for (var i = 0; i < subtitleStreams.Count; i++)
        {
            var stream = subtitleStreams[i];
            var labelParts = new List<string> { $"Track {i + 1}" };
            if (!string.IsNullOrWhiteSpace(stream.Language))
                labelParts.Add(stream.Language!);
            if (!string.IsNullOrWhiteSpace(stream.Title))
                labelParts.Add(stream.Title!);
            if (!string.IsNullOrWhiteSpace(stream.CodecName))
                labelParts.Add(stream.CodecName.ToUpperInvariant());

            var option = new SubtitleTrackOption(i, stream, string.Join(" · ", labelParts), isSelected: i == 0);
            option.SelectionChanged += SubtitleTrack_SelectionChanged;
            SubtitleTracks.Add(option);
        }

        OnPropertyChanged(nameof(SubtitleTracks));
        OnPropertyChanged(nameof(ShowSubtitleTrackSelector));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private void SubtitleTrack_SelectionChanged(object? sender, EventArgs e)
    {
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private string BuildSubtitleOutputPath(string extension)
    {
        var selectedCount = SubtitleTracks.Count(t => t.IsSelected);
        return selectedCount > 1
            ? Path.ChangeExtension(_outputFilePath!, extension + ".zip")
            : Path.ChangeExtension(_outputFilePath!, extension);
    }

    private IEnumerable<SubtitleExtractionTrack> BuildSelectedSubtitleExtractions(string? subtitleOutputPath)
    {
        if (subtitleOutputPath is null ||
            _selectedSubtitleMode is not (SubtitleMode.ExtractSrt or SubtitleMode.ExtractAss))
        {
            yield break;
        }

        var extension = _selectedSubtitleMode == SubtitleMode.ExtractSrt ? ".srt" : ".ass";
        var baseName = Path.GetFileNameWithoutExtension(_outputFilePath) ?? "subtitles";

        foreach (var track in SubtitleTracks.Where(t => t.IsSelected))
        {
            var language = string.IsNullOrWhiteSpace(track.Language) ? "und" : SanitizeFileName(track.Language!);
            var title = string.IsNullOrWhiteSpace(track.Title) ? "" : "." + SanitizeFileName(track.Title!);
            var fileName = $"{baseName}.track{track.SubtitleIndex + 1:00}.{language}{title}{extension}";
            yield return new SubtitleExtractionTrack(track.SubtitleIndex, track.StreamIndex, fileName);
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
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
