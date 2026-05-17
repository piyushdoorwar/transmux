using System.Text.RegularExpressions;
using System.IO.Compression;
using Transmux.Core.Models;

namespace Transmux.Core.Services;

public sealed class FfmpegService
{
    private static readonly Regex TimeRegex =
        new(@"time=(\d+):(\d+):(\d+\.\d+)", RegexOptions.Compiled);

    private static readonly Regex SpeedRegex =
        new(@"speed=\s*(\d+(?:\.\d+)?)x", RegexOptions.Compiled);

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task ConvertAsync(
        ConversionOptions options,
        IProgress<ConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        string? subtitleTempDir = null;
        IReadOnlyList<SubtitleOutputTarget> subtitleOutputs = [];

        if (ShouldExtractSubtitles(options) && options.SubtitleOutputPath is not null)
        {
            if (options.SubtitleTracks.Count > 1)
            {
                subtitleTempDir = Path.Combine(Path.GetTempPath(), "transmux-subs-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(subtitleTempDir);

                subtitleOutputs = options.SubtitleTracks
                    .Select(t => new SubtitleOutputTarget(t.SubtitleIndex, Path.Combine(subtitleTempDir, t.FileName)))
                    .ToList();
            }
            else
            {
                var track = options.SubtitleTracks.Count == 1
                    ? options.SubtitleTracks[0]
                    : new SubtitleExtractionTrack(0, 0, Path.GetFileName(options.SubtitleOutputPath));
                subtitleOutputs = [new SubtitleOutputTarget(track.SubtitleIndex, options.SubtitleOutputPath)];
            }
        }

        var args = BuildArguments(options, subtitleOutputs);
        var ffmpeg = ResolveBinary("ffmpeg");

        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            var startTime = DateTime.UtcNow;
            var stderrTail = new System.Collections.Generic.Queue<string>();

            _ = Task.Run(async () =>
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) is not null)
                {
                    lock (stderrTail)
                    {
                        stderrTail.Enqueue(line);
                        if (stderrTail.Count > 15) stderrTail.Dequeue();
                    }

                    if (progress is null || options.InputDuration == TimeSpan.Zero)
                        continue;

                    var timeMatch = TimeRegex.Match(line);
                    if (!timeMatch.Success) continue;

                    var h = int.Parse(timeMatch.Groups[1].Value);
                    var m = int.Parse(timeMatch.Groups[2].Value);
                    var s = double.Parse(timeMatch.Groups[3].Value,
                        System.Globalization.CultureInfo.InvariantCulture);

                    var current = TimeSpan.FromSeconds(h * 3600 + m * 60 + s);
                    var percent = Math.Min(100.0, current / options.InputDuration * 100.0);

                    var speedMatch = SpeedRegex.Match(line);
                    var speed = speedMatch.Success
                        ? double.Parse(speedMatch.Groups[1].Value,
                            System.Globalization.CultureInfo.InvariantCulture)
                        : 0;

                    var elapsed = DateTime.UtcNow - startTime;
                    TimeSpan? eta = null;
                    if (speed > 0 && percent > 0)
                    {
                        var remaining = options.InputDuration - current;
                        eta = speed > 0 ? remaining / speed : null;
                    }

                    progress.Report(new ConversionProgress(percent, speed, elapsed, eta));
                }
            }, cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            if (process.ExitCode != 0)
            {
                string detail;
                lock (stderrTail)
                    detail = string.Join("\n", stderrTail);

                throw new InvalidOperationException(
                    $"FFmpeg exited with code {process.ExitCode}.\n\n{detail}");
            }

            if (subtitleTempDir is not null && options.SubtitleOutputPath is not null)
            {
                if (File.Exists(options.SubtitleOutputPath))
                    File.Delete(options.SubtitleOutputPath);

                ZipFile.CreateFromDirectory(
                    subtitleTempDir,
                    options.SubtitleOutputPath,
                    CompressionLevel.Optimal,
                    includeBaseDirectory: false);
            }
        }
        finally
        {
            if (subtitleTempDir is not null && Directory.Exists(subtitleTempDir))
                Directory.Delete(subtitleTempDir, recursive: true);
        }
    }

    public static string ResolveBinary(string name)
    {
        // 1. Next to the app executable (bundled on Windows/macOS)
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, name);
        if (File.Exists(candidate)) return candidate;

        // Windows: try with .exe extension
        var candidateExe = Path.Combine(baseDir, name + ".exe");
        if (File.Exists(candidateExe)) return candidateExe;

        // 2. On PATH (Linux system ffmpeg, or user-installed)
        return name;
    }

    // ── Argument builder ──────────────────────────────────────────────────────

    private static string BuildArguments(
        ConversionOptions options,
        IReadOnlyList<SubtitleOutputTarget> subtitleOutputs)
    {
        var parts = new List<string>
        {
            "-y",
            $"-i {Q(options.InputPath)}"
        };

        // If we have audio track selection, map them; otherwise include all.
        // When using -map, we must also explicitly keep the video stream,
        // otherwise ffmpeg drops video entirely and the output is broken.
        if (options.AudioTracks.Count > 0)
        {
            parts.Add("-map 0:v?");
            foreach (var audioTrack in options.AudioTracks)
            {
                parts.Add($"-map 0:a:{audioTrack.AudioIndex}");
            }
        }

        if (options.FastConvert)
        {
            // Stream copy — no re-encoding; very fast but container must be compatible
            if (options.Format.IsAudioOnly)
            {
                parts.Add("-vn");
                parts.Add("-c:a copy");
            }
            else
            {
                parts.Add("-c copy");
            }
        }
        else if (options.Format.IsAudioOnly)
        {
            // Strip video, encode audio only
            parts.Add("-vn");
            parts.Add(options.Format.AudioArgs);
        }
        else
        {
            parts.Add(options.Format.VideoArgs);
            parts.Add(options.Format.AudioArgs);
        }

        // Subtitle handling
        switch (options.SubtitleMode)
        {
            case SubtitleMode.Include:
                // Copy subtitle streams that are compatible with the container
                if (options.Format.Extension == ".mp4" || options.Format.Extension == ".mov")
                    parts.Add("-c:s mov_text");
                else if (options.Format.Extension == ".mkv" || options.Format.Extension == ".webm")
                    parts.Add("-c:s copy");
                else
                    parts.Add("-sn"); // Container doesn't support subtitles
                break;

            case SubtitleMode.ExtractSrt:
            case SubtitleMode.ExtractAss:
                parts.Add("-sn"); // Subtitles go to separate file, not main output
                break;

            case SubtitleMode.None:
            default:
                parts.Add("-sn");
                break;
        }

        parts.Add(Q(options.OutputPath));

        // Subtitle extraction as a separate pass appended to the same ffmpeg invocation
        if (ShouldExtractSubtitles(options) && subtitleOutputs.Count > 0)
        {
            // Re-input the source for subtitle extraction after the main output
            parts.Add($"-i {Q(options.InputPath)}");
            foreach (var output in subtitleOutputs)
            {
                parts.Add($"-map 1:s:{output.SubtitleIndex}");
                parts.Add(Q(output.OutputPath));
            }
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static bool ShouldExtractSubtitles(ConversionOptions options) =>
        options.SubtitleMode is SubtitleMode.ExtractSrt or SubtitleMode.ExtractAss;

    private static string Q(string path) => $"\"{path.Replace("\"", "\\\"")}\"";

    private sealed record SubtitleOutputTarget(int SubtitleIndex, string OutputPath);
}
