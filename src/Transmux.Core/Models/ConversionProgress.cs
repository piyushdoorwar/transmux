namespace Transmux.Core.Models;

public sealed record ConversionProgress(
    double Percent,
    double Speed,
    TimeSpan Elapsed,
    TimeSpan? Eta);
