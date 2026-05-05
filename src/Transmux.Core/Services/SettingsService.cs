using System.Text.Json;

namespace Transmux.Core.Services;

public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Transmux",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private TransmuxSettings _settings = new();

    public SettingsService()
    {
        Load();
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string? LastOutputDirectory
    {
        get => _settings.LastOutputDirectory;
        set { _settings.LastOutputDirectory = value; Save(); }
    }

    public string? LastOutputFormatId
    {
        get => _settings.LastOutputFormatId;
        set { _settings.LastOutputFormatId = value; Save(); }
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            _settings = JsonSerializer.Deserialize<TransmuxSettings>(json, JsonOptions) ?? new();
        }
        catch
        {
            _settings = new TransmuxSettings();
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Settings persistence is best-effort; never crash the app.
        }
    }

    private sealed class TransmuxSettings
    {
        public string? LastOutputDirectory { get; set; }
        public string? LastOutputFormatId { get; set; }
    }
}


