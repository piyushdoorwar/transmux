using System.Text.Json;
using System.Text.Json.Nodes;
using Transmux.Core.Models;

namespace Transmux.Core.Services;

public sealed class MediaInspector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MediaInfo> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var ffprobe = FfmpegService.ResolveBinary("ffprobe");

        var args = $"-v quiet -print_format json -show_format -show_streams {Q(filePath)}";

        using var process = new System.Diagnostics.Process();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = ffprobe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var json = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(
                "Could not inspect the file. Make sure FFprobe is installed and the file is a valid media file.");

        return Parse(filePath, json);
    }

    private static MediaInfo Parse(string filePath, string json)
    {
        var root = JsonNode.Parse(json)
            ?? throw new InvalidOperationException("Invalid ffprobe output.");

        // Format
        var fmt = root["format"];
        var formatName = fmt?["format_name"]?.GetValue<string>() ?? "";
        var formatLongName = fmt?["format_long_name"]?.GetValue<string>() ?? "";
        var durationStr = fmt?["duration"]?.GetValue<string>();
        var duration = double.TryParse(durationStr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? TimeSpan.FromSeconds(d)
            : TimeSpan.Zero;

        // File size
        var fileSize = long.TryParse(fmt?["size"]?.GetValue<string>(), out var sz) ? sz : 0L;

        // Streams
        var streams = new List<StreamInfo>();
        if (root["streams"] is JsonArray streamsArray)
        {
            foreach (var s in streamsArray)
            {
                if (s is null) continue;
                var codecType = s["codec_type"]?.GetValue<string>() ?? "";
                var codecName = s["codec_name"]?.GetValue<string>() ?? "";
                var index = s["index"]?.GetValue<int>() ?? 0;
                var lang = s["tags"]?["language"]?.GetValue<string>();
                var title = s["tags"]?["title"]?.GetValue<string>();
                var bitRate = long.TryParse(s["bit_rate"]?.GetValue<string>(), out var br) ? br : 0L;

                var stream = new StreamInfo
                {
                    Index = index,
                    CodecType = codecType,
                    CodecName = codecName,
                    Language = lang,
                    Title = title,
                    BitRate = bitRate
                };

                if (codecType == "video")
                {
                    var width = s["width"]?.GetValue<int>() ?? 0;
                    var height = s["height"]?.GetValue<int>() ?? 0;
                    var fps = s["r_frame_rate"]?.GetValue<string>();
                    var profile = s["profile"]?.GetValue<string>();
                    var aspectRatio = s["display_aspect_ratio"]?.GetValue<string>();
                    stream = stream with
                    {
                        Width = width,
                        Height = height,
                        FrameRate = FormatFps(fps),
                        Profile = profile,
                        AspectRatio = aspectRatio
                    };
                }
                else if (codecType == "audio")
                {
                    var channels = s["channels"]?.GetValue<int>() ?? 0;
                    var sampleRate = int.TryParse(s["sample_rate"]?.GetValue<string>(), out var sr) ? sr : 0;
                    stream = stream with { Channels = channels, SampleRate = sampleRate };
                }

                streams.Add(stream);
            }
        }

        return new MediaInfo
        {
            FilePath = filePath,
            FormatName = formatName,
            FormatLongName = formatLongName,
            Duration = duration,
            FileSize = fileSize,
            Streams = streams.AsReadOnly()
        };
    }

    private static string? FormatFps(string? raw)
    {
        // r_frame_rate is a fraction like "24000/1001" or "30/1"
        if (raw is null) return null;
        var parts = raw.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var num) &&
            double.TryParse(parts[1], out var den) &&
            den > 0)
        {
            var fps = num / den;
            return fps % 1 == 0 ? $"{fps:F0}" : $"{fps:F3}";
        }
        return raw;
    }

    private static string Q(string path) => $"\"{path.Replace("\"", "\\\"")}\"";
}
