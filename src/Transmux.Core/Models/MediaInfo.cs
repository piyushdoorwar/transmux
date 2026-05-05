namespace Transmux.Core.Models;

public sealed class MediaInfo
{
    public string FilePath { get; init; } = "";
    public string FormatName { get; init; } = "";
    public string FormatLongName { get; init; } = "";
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<StreamInfo> Streams { get; init; } = [];

    public StreamInfo? VideoStream => Streams.FirstOrDefault(s => s.CodecType == "video");
    public IEnumerable<StreamInfo> AudioStreams => Streams.Where(s => s.CodecType == "audio");
    public IEnumerable<StreamInfo> SubtitleStreams => Streams.Where(s => s.CodecType == "subtitle");

    public bool HasVideo => VideoStream is not null;
    public bool HasSubtitles => SubtitleStreams.Any();
    public int SubtitleTrackCount => SubtitleStreams.Count();
}

public sealed record StreamInfo
{
    public int Index { get; init; }
    public string CodecType { get; init; } = "";
    public string CodecName { get; init; } = "";
    public string? Language { get; init; }

    // Video
    public int Width { get; init; }
    public int Height { get; init; }
    public string? FrameRate { get; init; }

    // Audio
    public int Channels { get; init; }
    public int SampleRate { get; init; }

    // Subtitle
    public string? Title { get; init; }
}
