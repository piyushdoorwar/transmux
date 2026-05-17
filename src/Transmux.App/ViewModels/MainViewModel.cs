using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Transmux.App.Models;
using Transmux.App.Services;
using Transmux.Core.Models;
using Transmux.Core.Services;

namespace Transmux.App.ViewModels;

public sealed record SubtitleModeOption(SubtitleMode Mode, string Label);

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

public sealed class AudioTrackOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public AudioTrackOption(int audioIndex, StreamInfo stream, string label, bool isSelected)
    {
        AudioIndex = audioIndex;
        StreamIndex = stream.Index;
        Language = stream.Language;
        Title = stream.Title;
        CodecName = stream.CodecName;
        Channels = stream.Channels;
        SampleRate = stream.SampleRate;
        Label = label;
        _isSelected = isSelected;
    }

    public int AudioIndex { get; }
    public int StreamIndex { get; }
    public string? Language { get; }
    public string? Title { get; }
    public string CodecName { get; }
    public int Channels { get; }
    public int SampleRate { get; }
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
    private readonly MediaInspector _inspector;
    private readonly SettingsService _settings;
    private readonly HistoryService _history;

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

    public MainViewModel(FfmpegService ffmpeg, MediaInspector inspector, SettingsService settings, HistoryService history)
    {
        _ffmpeg = ffmpeg;
        _inspector = inspector;
        _settings = settings;
        _history = history;

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
        ToggleAllSubtitlesCommand = new RelayCommand(_ => ToggleAllSubtitles());
        ToggleAllAudioCommand = new RelayCommand(_ => ToggleAllAudio());
        ToggleSubtitleTracksDropdownCommand = new RelayCommand(_ => ShowSubtitleTracksDropdown = !ShowSubtitleTracksDropdown);
        ToggleAudioTracksDropdownCommand = new RelayCommand(_ => ShowAudioTracksDropdown = !ShowAudioTracksDropdown);
        ShowHistoryCommand = new RelayCommand(async _ => await OpenHistoryDialogAsync());
        ShowKeyboardShortcutsCommand = new RelayCommand(async _ => await OpenKeyboardShortcutsDialogAsync());
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    public IReadOnlyList<FormatInfo> Formats { get; }
    public IReadOnlyList<SubtitleModeOption> SubtitleModes { get; }
    public List<SubtitleTrackOption> SubtitleTracks { get; } = [];
    public List<AudioTrackOption> AudioTracks { get; } = [];
    private SubtitleModeOption? _selectedSubtitleModeOption;

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
        $"{_detectedMedia.FormatLongName}  ·  {FormatDuration(_detectedMedia.Duration)}  ·  {FormatFileSize(_detectedMedia.FileSize)}  ·  {FormatBitrate(_detectedMedia.OverallBitRate)}";

    public string MediaSummaryVideo
    {
        get
        {
            var v = _detectedMedia?.VideoStream;
            if (v is null) return "⊘ No video stream (audio-only)";
            var res = v.Width > 0 ? $"  ·  {v.Width}×{v.Height}" : "";
            var fps = v.FrameRate is not null ? $"  ·  {v.FrameRate} fps" : "";
            var bitrate = v.BitRate > 0 ? $"  ·  {FormatBitrate(v.BitRate)}" : "";
            var profile = !string.IsNullOrWhiteSpace(v.Profile) ? $"  ·  {v.Profile}" : "";
            var aspect = !string.IsNullOrWhiteSpace(v.AspectRatio) ? $"  ·  {v.AspectRatio}" : "";
            return $"{v.CodecName.ToUpperInvariant()}{res}{fps}{bitrate}{profile}{aspect}";
        }
    }

    public string MediaSummaryAudio
    {
        get
        {
            var streams = _detectedMedia?.AudioStreams.ToList();
            if (streams is null || streams.Count == 0) return "No audio stream";
            var first = streams[0];
            var ch = FormatChannels(first.Channels);
            var sr = first.SampleRate > 0 ? $"  ·  {first.SampleRate / 1000.0:G3} kHz" : "";
            var br = first.BitRate > 0 ? $"  ·  {FormatBitrate(first.BitRate)}" : "";
            var extra = streams.Count > 1 ? $"  (+{streams.Count - 1} more)" : "";
            return $"{first.CodecName.ToUpperInvariant()}  ·  {ch}{sr}{br}{extra}";
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

    public bool ShowSubtitleTrackSelector =>
        _detectedMedia?.SubtitleTrackCount > 1 &&
        SelectedSubtitleMode is SubtitleMode.ExtractSrt or SubtitleMode.ExtractAss;

    public bool ShowAudioTrackSelector => _detectedMedia is not null && _detectedMedia.AudioStreams.Count() > 1;

    public bool AllAudioSelected => AudioTracks.Count > 0 && AudioTracks.All(t => t.IsSelected);

    public bool? AudioCheckState => AudioTracks.Count == 0 ? false : (AllAudioSelected ? true : (AudioTracks.Any(t => t.IsSelected) ? null : false));

    public bool AllSubtitlesSelected => SubtitleTracks.Count > 0 && SubtitleTracks.All(t => t.IsSelected);

    public bool? SubtitleCheckState => SubtitleTracks.Count == 0 ? false : (AllSubtitlesSelected ? true : (SubtitleTracks.Any(t => t.IsSelected) ? null : false));

    private bool _showSubtitleTracksDropdown;
    public bool ShowSubtitleTracksDropdown
    {
        get => _showSubtitleTracksDropdown;
        set => SetField(ref _showSubtitleTracksDropdown, value);
    }

    public string SubtitleTracksDisplayText
    {
        get
        {
            var selected = SubtitleTracks.Count(t => t.IsSelected);
            if (selected == 0) return "None selected";
            if (selected == SubtitleTracks.Count) return "All subtitles";
            if (selected == 1)
            {
                var track = SubtitleTracks.First(t => t.IsSelected);
                return track.Label;
            }
            return $"{selected} subtitles";
        }
    }

    private bool _showAudioTracksDropdown;
    public bool ShowAudioTracksDropdown
    {
        get => _showAudioTracksDropdown;
        set => SetField(ref _showAudioTracksDropdown, value);
    }

    public string AudioTracksDisplayText
    {
        get
        {
            var selected = AudioTracks.Count(t => t.IsSelected);
            if (selected == 0) return "None selected";
            if (selected == AudioTracks.Count) return "All audio tracks";
            if (selected == 1)
            {
                var track = AudioTracks.First(t => t.IsSelected);
                return track.Label;
            }
            return $"{selected} audio tracks";
        }
    }

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
                    OutputFilePath = EnsureDistinctFromInput(Path.ChangeExtension(_outputFilePath, value.Extension));
                ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
            }
        }
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
        HasRequiredSubtitleSelection &&
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
    public ICommand ToggleAllSubtitlesCommand { get; }
    public ICommand ToggleAllAudioCommand { get; }
    public ICommand ToggleSubtitleTracksDropdownCommand { get; }
    public ICommand ToggleAudioTracksDropdownCommand { get; }
    public ICommand ShowHistoryCommand { get; }
    public ICommand ShowKeyboardShortcutsCommand { get; }

    // Injected by the Window after construction
    public IStorageProvider? StorageProvider { get; set; }
    public Func<Task>? ShowAboutDialog { get; set; }
    public Func<Task>? ShowHistoryDialog { get; set; }
    public Func<Task>? ShowKeyboardShortcutsDialog { get; set; }
    public Func<Task<bool>>? ShowSkipConversionDialog { get; set; } // Returns true to skip, false to convert anyway

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
            BuildAudioTracks(info);

            // Smart format selection based on input codec
            var suggestedFormat = SuggestBestFormat(info);
            if (suggestedFormat is not null)
                SelectedFormat = suggestedFormat;

            // Default output path: same dir as input, same name, new extension
            var dir = Path.GetDirectoryName(filePath) ?? "";
            var name = Path.GetFileNameWithoutExtension(filePath);
            var ext = _selectedFormat?.Extension ?? ".mp4";
            OutputFilePath = EnsureDistinctFromInput(Path.Combine(dir, name + ext));

            // Default subtitle mode
            var targetMode = info.HasSubtitles ? SubtitleMode.Include : SubtitleMode.None;
            SelectedSubtitleModeOption = SubtitleModes.FirstOrDefault(m => m.Mode == targetMode) ?? SubtitleModes[0];

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

        // Check if source already matches target format
        if (DoesSourceMatchTarget(_detectedMedia, _selectedFormat))
        {
            if (ShowSkipConversionDialog is not null)
            {
                var skip = await ShowSkipConversionDialog();
                if (skip)
                    return;
            }
        }

        IsConverting = true;
        IsComplete = false;
        ErrorMessage = null;
        ProgressPercent = 0;
        SpeedText = "";
        EtaText = "";

        _cts = new CancellationTokenSource();

        string? subOutputPath = null;
        if (_selectedSubtitleMode == SubtitleMode.ExtractSrt)
            subOutputPath = BuildSubtitleOutputPath(".srt");
        else if (_selectedSubtitleMode == SubtitleMode.ExtractAss)
            subOutputPath = BuildSubtitleOutputPath(".ass");

        var subtitleTracks = BuildSelectedSubtitleExtractions(subOutputPath).ToList();
        var audioTracks = BuildSelectedAudioTracks().ToList();

        var options = new ConversionOptions(
            InputPath: _inputFilePath,
            OutputPath: _outputFilePath,
            SubtitleOutputPath: subOutputPath,
            SubtitleTracks: subtitleTracks,
            AudioTracks: audioTracks,
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

    private async Task OpenHistoryDialogAsync()
    {
        if (ShowHistoryDialog is not null)
            await ShowHistoryDialog();
    }

    private async Task OpenKeyboardShortcutsDialogAsync()
    {
        if (ShowKeyboardShortcutsDialog is not null)
            await ShowKeyboardShortcutsDialog();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatDuration(TimeSpan t)
    {
        if (t == TimeSpan.Zero) return "Unknown";
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:F1} {sizes[order]}";
    }

    private static string FormatBitrate(long bps)
    {
        if (bps <= 0) return "0 kbps";
        var kbps = bps / 1000.0;
        return kbps >= 1000 ? $"{kbps / 1000:F1} Mbps" : $"{kbps:F0} kbps";
    }

    private static string FormatChannels(int channels)
    {
        return channels switch
        {
            1 => "mono",
            2 => "stereo",
            6 => "5.1",
            8 => "7.1",
            _ => $"{channels}ch"
        };
    }

    // Suggest the best output format based on input codec
    private FormatInfo? SuggestBestFormat(MediaInfo info)
    {
        if (!info.HasVideo)
        {
            // Audio-only: match the codec
            var audioCodec = info.AudioStreams.FirstOrDefault()?.CodecName ?? "";
            return audioCodec.ToLower() switch
            {
                var c when c.Contains("aac") => OutputFormats.Aac,
                var c when c.Contains("opus") => OutputFormats.Opus,
                var c when c.Contains("vorbis") => OutputFormats.Ogg,
                var c when c.Contains("flac") => OutputFormats.Flac,
                var c when c.Contains("wav") => OutputFormats.Wav,
                _ => OutputFormats.Mp3
            };
        }

        // Video: check codec compatibility
        var videoCodec = info.VideoStream?.CodecName ?? "";
        var audioCodec2 = info.AudioStreams.FirstOrDefault()?.CodecName ?? "";

        return (videoCodec.ToLower(), audioCodec2.ToLower()) switch
        {
            // H.264 + AAC → MP4
            (var v, var a) when v.Contains("h264") && a.Contains("aac") => OutputFormats.Mp4H264,

            // VP9 + Opus → WebM
            (var v, var a) when v.Contains("vp9") && a.Contains("opus") => OutputFormats.WebM,

            // Any combination → MKV (copy, no re-encode needed)
            _ => OutputFormats.MkvCopy
        };
    }

    // Check if source format already matches target
    private bool DoesSourceMatchTarget(MediaInfo info, FormatInfo targetFormat)
    {
        if (!info.HasVideo || _inputFilePath is null)
            return false; // Audio-only files need conversion for now

        var videoCodec = info.VideoStream?.CodecName ?? "";
        var audioCodec = info.AudioStreams.FirstOrDefault()?.CodecName ?? "";
        var containerExt = Path.GetExtension(_inputFilePath).ToLower();

        // Check if it's MP4 with H.264 + AAC
        if (targetFormat == OutputFormats.Mp4H264 &&
            containerExt == ".mp4" &&
            videoCodec.Contains("h264") &&
            audioCodec.Contains("aac"))
            return true;

        // Check if it's WebM with VP9 + Opus
        if (targetFormat == OutputFormats.WebM &&
            containerExt == ".webm" &&
            videoCodec.Contains("vp9") &&
            audioCodec.Contains("opus"))
            return true;

        return false;
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
        OnPropertyChanged(nameof(SubtitleTracksDisplayText));
        OnPropertyChanged(nameof(ShowSubtitleTrackSelector));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private void SubtitleTrack_SelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SubtitleCheckState));
        OnPropertyChanged(nameof(SubtitleTracksDisplayText));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private void BuildAudioTracks(MediaInfo info)
    {
        foreach (var track in AudioTracks)
            track.SelectionChanged -= AudioTrack_SelectionChanged;

        AudioTracks.Clear();

        var audioStreams = info.AudioStreams.ToList();
        for (var i = 0; i < audioStreams.Count; i++)
        {
            var stream = audioStreams[i];
            var labelParts = new List<string> { $"Track {i + 1}" };
            if (!string.IsNullOrWhiteSpace(stream.Language))
                labelParts.Add(stream.Language!);
            if (!string.IsNullOrWhiteSpace(stream.Title))
                labelParts.Add(stream.Title!);
            var ch = stream.Channels switch { 1 => "mono", 2 => "stereo", var n => $"{n}ch" };
            if (!string.IsNullOrWhiteSpace(ch))
                labelParts.Add(ch);
            if (!string.IsNullOrWhiteSpace(stream.CodecName))
                labelParts.Add(stream.CodecName.ToUpperInvariant());

            var option = new AudioTrackOption(i, stream, string.Join(" · ", labelParts), isSelected: i == 0);
            option.SelectionChanged += AudioTrack_SelectionChanged;
            AudioTracks.Add(option);
        }

        OnPropertyChanged(nameof(AudioTracks));
        OnPropertyChanged(nameof(AudioTracksDisplayText));
        OnPropertyChanged(nameof(ShowAudioTrackSelector));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private void AudioTrack_SelectionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AudioCheckState));
        OnPropertyChanged(nameof(AudioTracksDisplayText));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private void ToggleAllSubtitles()
    {
        var allSelected = AllSubtitlesSelected;
        foreach (var track in SubtitleTracks)
            track.IsSelected = !allSelected;
        OnPropertyChanged(nameof(AllSubtitlesSelected));
        OnPropertyChanged(nameof(SubtitleCheckState));
        OnPropertyChanged(nameof(SubtitleTracksDisplayText));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    private void ToggleAllAudio()
    {
        var allSelected = AllAudioSelected;
        foreach (var track in AudioTracks)
            track.IsSelected = !allSelected;
        OnPropertyChanged(nameof(AllAudioSelected));
        OnPropertyChanged(nameof(AudioCheckState));
        OnPropertyChanged(nameof(AudioTracksDisplayText));
        ((RelayCommand)StartConversionCommand).RaiseCanExecuteChanged();
    }

    // FFmpeg cannot edit a file in-place. If the chosen output path collides
    // with the input (e.g. input.mkv → MKV output), append a suffix.
    private string EnsureDistinctFromInput(string candidate)
    {
        if (_inputFilePath is null ||
            !string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(_inputFilePath),
                StringComparison.OrdinalIgnoreCase))
            return candidate;

        var dir = Path.GetDirectoryName(candidate) ?? "";
        var name = Path.GetFileNameWithoutExtension(candidate);
        var ext = Path.GetExtension(candidate);
        return Path.Combine(dir, name + " (converted)" + ext);
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

    private IEnumerable<AudioTrackSelection> BuildSelectedAudioTracks()
    {
        foreach (var track in AudioTracks.Where(t => t.IsSelected))
        {
            yield return new AudioTrackSelection(track.AudioIndex, track.StreamIndex);
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

}
