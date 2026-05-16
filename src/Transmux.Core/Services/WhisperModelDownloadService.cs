using System.IO.Compression;
using System.Diagnostics;

namespace Transmux.Core.Services;

/// <summary>
/// Handles downloading and caching Whisper models.
/// Models are downloaded from the official Hugging Face repository.
/// </summary>
public sealed class WhisperModelDownloadService
{
    private readonly string _modelDir;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(30) };

    public WhisperModelDownloadService(string? customModelDir = null)
    {
        _modelDir = customModelDir ?? GetDefaultModelDirectory();
    }

    /// <summary>
    /// Gets or downloads the specified model.
    /// Returns the path to the model file if successful, null otherwise.
    /// </summary>
    public async Task<string?> GetOrDownloadModelAsync(
        string modelName,
        IProgress<(long downloaded, long total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Check if model already exists locally
        var existingPath = FindLocalModel(modelName);
        if (existingPath is not null)
            return existingPath;

        // Download the model
        return await DownloadModelAsync(modelName, progress, cancellationToken);
    }

    /// <summary>
    /// Lists all available models.
    /// </summary>
    public static IEnumerable<string> AvailableModels =>
        new[] { "tiny", "base", "small", "medium", "large-v3", "large-v3-turbo" };

    /// <summary>
    /// Gets the file size of a model if available remotely (for progress display).
    /// </summary>
    public static async Task<long> GetModelSizeAsync(string modelName, CancellationToken cancellationToken = default)
    {
        var url = GetModelUrl(modelName);

        try
        {
            using var response = await Client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, url),
                cancellationToken);

            if (response.IsSuccessStatusCode &&
                response.Content.Headers.ContentLength.HasValue)
            {
                return response.Content.Headers.ContentLength.Value;
            }
        }
        catch { /* ignore */ }

        return -1;
    }

    public string? FindLocalModel(string modelName)
    {
        foreach (var candidate in GetModelFileNames(modelName))
        {
            var path = Path.Combine(_modelDir, candidate);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public bool IsModelDownloaded(string modelName) => FindLocalModel(modelName) is not null;

    public IEnumerable<string> GetDownloadedModels()
    {
        if (!Directory.Exists(_modelDir))
            return [];

        var downloaded = new HashSet<string>();

        foreach (var file in Directory.GetFiles(_modelDir, "ggml-*.bin"))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var modelName = fileName.Replace("ggml-", "").TrimEnd(".en".ToCharArray());
            downloaded.Add(modelName);
        }

        return downloaded;
    }

    private async Task<string?> DownloadModelAsync(
        string modelName,
        IProgress<(long downloaded, long total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_modelDir);

        var url = GetModelUrl(modelName);
        var fileName = $"ggml-{modelName}.bin";
        var filePath = Path.Combine(_modelDir, fileName);

        if (File.Exists(filePath))
            return filePath;

        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var canReportProgress = totalBytes != -1;

            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var fileStream = File.Create(filePath))
            {
                var totalRead = 0L;
                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalRead += bytesRead;

                    if (canReportProgress)
                        progress?.Report((totalRead, totalBytes));
                }
            }

            return filePath;
        }
        catch (Exception ex)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            throw new InvalidOperationException($"Failed to download model {modelName}: {ex.Message}", ex);
        }
    }

    private static string GetModelUrl(string modelName)
    {
        // Using the official OpenAI Whisper model URLs hosted on Hugging Face
        var baseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/models";
        var fileName = $"ggml-{modelName}.bin";

        // For English-specific models
        if (modelName.EndsWith(".en"))
            fileName = $"ggml-{modelName.Replace(".en", "")}.en.bin";

        return $"{baseUrl}/{fileName}";
    }

    private static IEnumerable<string> GetModelFileNames(string modelName)
    {
        yield return $"ggml-{modelName}.bin";
        yield return $"ggml-{modelName}.en.bin";
        yield return $"{modelName}.bin";
    }

    private static string GetDefaultModelDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var modelDir = Path.Combine(home, ".local", "share", "Transmux", "whisper", "models");

        // On Windows, prefer LocalApplicationData
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            modelDir = Path.Combine(appData, "Transmux", "whisper", "models");
        }

        return modelDir;
    }

    public long GetLocalModelSize(string modelName)
    {
        var path = FindLocalModel(modelName);
        return path is not null && File.Exists(path)
            ? new FileInfo(path).Length
            : -1;
    }

    public void DeleteModel(string modelName)
    {
        var path = FindLocalModel(modelName);
        if (path is not null && File.Exists(path))
            File.Delete(path);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "Unknown";

        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
