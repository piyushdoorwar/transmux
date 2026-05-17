using System.Text.Json;
using Transmux.App.Models;

namespace Transmux.App.Services;

public sealed class HistoryService
{
    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Transmux",
        "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<ConversionRecord> _history = [];

    public HistoryService()
    {
        Load();
    }

    public IReadOnlyList<ConversionRecord> History => _history.AsReadOnly();

    public void AddRecord(string inputFileName, string outputFileName, string outputPath, string format, bool success)
    {
        var record = new ConversionRecord
        {
            InputFileName = inputFileName,
            OutputFileName = outputFileName,
            OutputPath = outputPath,
            Format = format,
            Timestamp = DateTime.Now,
            Status = success ? "Completed" : "Failed"
        };

        _history.Insert(0, record); // Add to the beginning (most recent first)

        // Keep only last 50 records
        if (_history.Count > 50)
            _history.RemoveAt(_history.Count - 1);

        Save();
    }

    public void ClearHistory()
    {
        _history.Clear();
        Save();
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void Load()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return;
            var json = File.ReadAllText(HistoryPath);
            _history = JsonSerializer.Deserialize<List<ConversionRecord>>(json, JsonOptions) ?? [];
        }
        catch
        {
            _history = [];
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            File.WriteAllText(HistoryPath, json);
        }
        catch
        {
            // History persistence is best-effort; never crash the app.
        }
    }
}
