using System.Diagnostics;
using System.Text.RegularExpressions;
using Transmux.Core.Models;

namespace Transmux.Core.Services;

public sealed class WhisperSubtitleService
{
    private static readonly Regex FfmpegTimeRegex =
        new(@"time=(\d+):(\d+):(\d+\.\d+)", RegexOptions.Compiled);

    private static readonly Regex WhisperProgressRegex =
        new(@"progress\s*=\s*(\d+)%", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly WhisperSetupService _setup;
    private readonly WhisperModelDownloadService _modelDownload;

    public WhisperSubtitleService(
        WhisperSetupService? setup = null,
        WhisperModelDownloadService? modelDownload = null)
    {
        _setup = setup ?? new WhisperSetupService();
        _modelDownload = modelDownload ?? new WhisperModelDownloadService();
    }

    public async Task GenerateSrtAsync(
        SubtitleGenerationOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken,
        IProgress<string>? setupProgress = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "transmux-whisper-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var wavPath = Path.Combine(tempDir, "input.wav");

        try
        {
            setupProgress?.Report("Checking Whisper installation...");
            if (!_setup.IsWhisperAvailable())
            {
                setupProgress?.Report("Installing Whisper...");
                if (!await _setup.EnsureWhisperCppInstalledAsync(setupProgress))
                    throw new InvalidOperationException(
                        "Whisper is not installed and automatic installation failed. " +
                        "Please install whisper.cpp manually from https://github.com/ggerganov/whisper.cpp " +
                        "or run: pip install openai-whisper");
            }

            var runner = _setup.GetAvailableRunner();

            // whisper.cpp needs a ggml model file; Python whisper manages its own models
            string? modelPath = null;
            if (runner == WhisperRunner.WhisperCpp)
            {
                setupProgress?.Report($"Checking {options.Model.DisplayName} model...");
                modelPath = await _modelDownload.GetOrDownloadModelAsync(
                    options.Model.WhisperCppModelName,
                    null,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                    throw new InvalidOperationException(
                        $"Could not download {options.Model.DisplayName} model. " +
                        "Please check your internet connection and try again.");
            }
            else
            {
                setupProgress?.Report($"Using Python whisper with '{options.Model.PythonModelName}' model (downloads on first use)...");
            }

            setupProgress?.Report("Ready — generating subtitles");
            progress?.Report(new ConversionProgress(1, 0, TimeSpan.Zero, null));

            await ExtractAudioAsync(options, wavPath, progress, cancellationToken);

            if (runner == WhisperRunner.WhisperCpp)
                await RunWhisperCppAsync(options, wavPath, tempDir, modelPath!, progress, cancellationToken);
            else
                await RunPythonWhisperAsync(options, wavPath, tempDir, progress, cancellationToken);

            progress?.Report(new ConversionProgress(100, 0, TimeSpan.Zero, null));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Audio extraction ───────────────────────────────────────────────────────

    private static async Task ExtractAudioAsync(
        SubtitleGenerationOptions options,
        string wavPath,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var ffmpeg = FfmpegService.ResolveBinary("ffmpeg");
        var args = $"-y -i {Q(options.InputPath)} -vn -ar 16000 -ac 1 -c:a pcm_s16le {Q(wavPath)}";

        await RunProcessAsync(
            ffmpeg, args,
            line =>
            {
                if (progress is null || options.InputDuration == TimeSpan.Zero) return;
                var match = FfmpegTimeRegex.Match(line);
                if (!match.Success) return;
                var h = int.Parse(match.Groups[1].Value);
                var m = int.Parse(match.Groups[2].Value);
                var s = double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                var pct = Math.Min(35.0, TimeSpan.FromSeconds(h * 3600 + m * 60 + s) / options.InputDuration * 35.0);
                progress.Report(new ConversionProgress(pct, 0, TimeSpan.Zero, null));
            },
            "FFmpeg could not extract audio for subtitle generation.",
            cancellationToken);
    }

    // ── whisper.cpp runner ─────────────────────────────────────────────────────

    private static async Task RunWhisperCppAsync(
        SubtitleGenerationOptions options,
        string wavPath,
        string tempDir,
        string modelPath,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var whisperCpp = WhisperSetupService.TryResolveWhisperCppBinary()
            ?? throw new InvalidOperationException("whisper-cli not found.");

        var outputBase = Path.Combine(tempDir, "subtitles");
        var args = $"-m {Q(modelPath)} -f {Q(wavPath)} -osrt -of {Q(outputBase)} -t {options.ThreadCount} --print-progress";

        if (!string.IsNullOrWhiteSpace(options.Language))
            args += $" -l {Q(options.Language)}";

        await RunProcessAsync(
            whisperCpp, args,
            line => ReportWhisperCppProgress(line, progress),
            "whisper-cli could not generate subtitles.",
            cancellationToken);

        MoveGeneratedSrt(outputBase + ".srt", options.OutputPath);
    }

    // ── Python whisper runner ──────────────────────────────────────────────────

    private static async Task RunPythonWhisperAsync(
        SubtitleGenerationOptions options,
        string wavPath,
        string tempDir,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var whisper = WhisperSetupService.TryResolvePythonWhisperBinary()
            ?? throw new InvalidOperationException("Python whisper not found.");

        // Python whisper: whisper <file> --model <name> --output_format srt --output_dir <dir>
        var args = $"{Q(wavPath)} --model {options.Model.PythonModelName} --output_format srt --output_dir {Q(tempDir)}";

        if (!string.IsNullOrWhiteSpace(options.Language))
            args += $" --language {options.Language}";

        // Python whisper doesn't emit structured progress — report midpoint while running
        progress?.Report(new ConversionProgress(50, 0, TimeSpan.Zero, null));

        await RunProcessAsync(
            whisper, args,
            _ => { },
            "Python whisper could not generate subtitles.",
            cancellationToken);

        // Python whisper names the output after the input file basename
        var srtName = Path.GetFileNameWithoutExtension(wavPath) + ".srt";
        MoveGeneratedSrt(Path.Combine(tempDir, srtName), options.OutputPath);
    }

    // ── Shared process runner ──────────────────────────────────────────────────

    private static async Task RunProcessAsync(
        string fileName,
        string arguments,
        Action<string> onLine,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var recentOutput = new Queue<string>();
        var outputLock = new object();

        void CaptureLine(string line)
        {
            lock (outputLock)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    recentOutput.Enqueue(line.Trim());
                    while (recentOutput.Count > 6)
                        recentOutput.Dequeue();
                }
            }
            onLine(line);
        }

        var stdout = ReadLinesAsync(process.StandardOutput, CaptureLine, cancellationToken);
        var stderr = ReadLinesAsync(process.StandardError, CaptureLine, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        if (process.ExitCode != 0)
        {
            string details;
            lock (outputLock)
                details = string.Join(Environment.NewLine, recentOutput);

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? failureMessage
                    : failureMessage + Environment.NewLine + details);
        }
    }

    private static async Task ReadLinesAsync(
        StreamReader reader,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            onLine(line);
    }

    private static void ReportWhisperCppProgress(string line, IProgress<ConversionProgress>? progress)
    {
        if (progress is null) return;
        var match = WhisperProgressRegex.Match(line);
        if (!match.Success) return;
        var pct = 35.0 + Math.Min(100.0, double.Parse(match.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture)) * 0.64;
        progress.Report(new ConversionProgress(pct, 0, TimeSpan.Zero, null));
    }

    private static void MoveGeneratedSrt(string generatedPath, string outputPath)
    {
        if (!File.Exists(generatedPath))
            throw new InvalidOperationException("Whisper finished but did not create an SRT file.");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir))
            Directory.CreateDirectory(outputDir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        File.Move(generatedPath, outputPath);
    }

    private static string Q(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
