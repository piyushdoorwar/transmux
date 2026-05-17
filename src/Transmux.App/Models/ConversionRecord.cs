namespace Transmux.App.Models;

public sealed record ConversionRecord
{
    public string InputFileName { get; init; } = "";
    public string OutputFileName { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public string Format { get; init; } = "";
    public DateTime Timestamp { get; init; }
    public string Status { get; init; } = "Completed"; // Completed or Failed
}
