namespace Transmux.Core.Models;

public enum SubtitleMode
{
    Include,    // embed subtitle tracks in the output container
    ExtractSrt, // extract selected subtitle track(s) to .srt file(s)
    ExtractAss, // extract selected subtitle track(s) to .ass file(s)
    None        // strip all subtitle tracks from the output
}

public sealed record FormatInfo(
    string Id,
    string DisplayName,
    string Extension,
    bool IsAudioOnly,
    string VideoArgs,
    string AudioArgs);

public static class OutputFormats
{
    public static readonly FormatInfo Mp4H264 = new(
        "mp4_h264", "MP4 (H.264 / AAC)", ".mp4", false,
        "-c:v libx264 -crf 23 -preset fast", "-c:a aac -b:a 192k");

    public static readonly FormatInfo WebM = new(
        "webm_vp9", "WebM (VP9 / Opus)", ".webm", false,
        "-c:v libvpx-vp9 -crf 33 -b:v 0", "-c:a libopus -b:a 128k");

    public static readonly FormatInfo MkvCopy = new(
        "mkv_copy", "MKV (copy streams)", ".mkv", false,
        "-c:v copy", "-c:a copy");

    public static readonly FormatInfo Avi = new(
        "avi", "AVI (H.264 / MP3)", ".avi", false,
        "-c:v libx264 -crf 23 -preset fast", "-c:a libmp3lame -q:a 2");

    public static readonly FormatInfo Mov = new(
        "mov", "MOV (H.264 / AAC)", ".mov", false,
        "-c:v libx264 -crf 23 -preset fast", "-c:a aac -b:a 192k");

    public static readonly FormatInfo Mp3 = new(
        "mp3", "MP3 (audio only)", ".mp3", true,
        "", "-c:a libmp3lame -q:a 2");

    public static readonly FormatInfo Aac = new(
        "aac", "AAC / M4A (audio only)", ".m4a", true,
        "", "-c:a aac -b:a 256k");

    public static readonly FormatInfo Flac = new(
        "flac", "FLAC (lossless audio)", ".flac", true,
        "", "-c:a flac");

    public static readonly FormatInfo Ogg = new(
        "ogg", "OGG Vorbis (audio only)", ".ogg", true,
        "", "-c:a libvorbis -q:a 6");

    public static readonly FormatInfo Wav = new(
        "wav", "WAV (uncompressed audio)", ".wav", true,
        "", "-c:a pcm_s16le");

    public static readonly FormatInfo Opus = new(
        "opus", "Opus (audio only)", ".opus", true,
        "", "-c:a libopus -b:a 128k");

    public static readonly IReadOnlyList<FormatInfo> All =
        [Mp4H264, WebM, MkvCopy, Avi, Mov, Mp3, Aac, Flac, Ogg, Wav, Opus];
}

public sealed record SubtitleExtractionTrack(
    int SubtitleIndex,
    int StreamIndex,
    string FileName);

public sealed record ConversionOptions(
    string InputPath,
    string OutputPath,
    string? SubtitleOutputPath,
    IReadOnlyList<SubtitleExtractionTrack> SubtitleTracks,
    FormatInfo Format,
    SubtitleMode SubtitleMode,
    TimeSpan InputDuration,
    bool FastConvert = false);
