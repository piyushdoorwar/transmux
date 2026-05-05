using Transmux.Core.Models;

namespace Transmux.App.Models;

/// <summary>Snapshot of the user's configured conversion job, captured at the moment Convert is pressed.</summary>
public sealed record ConversionJob(
    string InputPath,
    string OutputPath,
    string? SubtitleOutputPath,
    FormatInfo Format,
    SubtitleMode SubtitleMode,
    TimeSpan InputDuration);
