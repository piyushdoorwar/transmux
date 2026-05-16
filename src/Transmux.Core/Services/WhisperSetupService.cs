using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Transmux.Core.Services;

public enum WhisperRunner { None, WhisperCpp, PythonWhisper }

/// <summary>
/// Handles installation and setup of a local Whisper runner (whisper.cpp or Python openai-whisper).
/// </summary>
public sealed class WhisperSetupService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<bool> EnsureWhisperCppInstalledAsync(IProgress<string>? progress = null)
    {
        progress?.Report("Checking for Whisper...");

        if (IsWhisperAvailable())
        {
            progress?.Report("Whisper is already installed.");
            return true;
        }

        progress?.Report("Whisper not found. Attempting installation...");

        try
        {
            if (await TryInstallWhisperAsync(progress) && IsWhisperAvailable())
            {
                progress?.Report("Whisper installed successfully!");
                return true;
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Installation error: {ex.Message}");
        }

        progress?.Report("Could not install Whisper automatically. Please install whisper.cpp from https://github.com/ggerganov/whisper.cpp or run: pip install openai-whisper");
        return false;
    }

    public bool IsWhisperAvailable() => GetAvailableRunner() != WhisperRunner.None;

    public WhisperRunner GetAvailableRunner()
    {
        if (TryResolveWhisperCppBinary() is not null)
            return WhisperRunner.WhisperCpp;
        if (TryResolvePythonWhisperBinary() is not null)
            return WhisperRunner.PythonWhisper;
        return WhisperRunner.None;
    }

    // Returns the full path to whisper-cli, or null if not found.
    internal static string? TryResolveWhisperCppBinary()
    {
        var binaryName = OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli";

        // 1. Transmux app install dir
        var appCandidate = Path.Combine(GetAppBinDir(), binaryName);
        if (File.Exists(appCandidate))
            return appCandidate;

        // 2. Next to the app executable (bundled deployment)
        var baseCandidate = Path.Combine(AppContext.BaseDirectory, binaryName);
        if (File.Exists(baseCandidate))
            return baseCandidate;

        // 3. System PATH
        return FindOnPath("whisper-cli");
    }

    // Returns the full path to the Python `whisper` binary, or null.
    internal static string? TryResolvePythonWhisperBinary()
    {
        // Check PATH first
        var onPath = FindOnPath("whisper");
        if (onPath is not null)
            return onPath;

        // Common pip --user install locations
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = OperatingSystem.IsWindows()
            ? new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Python", "Scripts", "whisper.exe"),
                Path.Combine(home, "AppData", "Local", "Programs", "Python", "Scripts", "whisper.exe")
            }
            : new[]
            {
                Path.Combine(home, ".local", "bin", "whisper"),
                "/usr/local/bin/whisper",
                "/usr/bin/whisper"
            };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindOnPath(string binaryName)
    {
        string[] extensions = OperatingSystem.IsWindows()
            ? [".exe", ".cmd", ".bat", ""]
            : [""];

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, binaryName + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    internal static string GetAppBinDir()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "Transmux", "bin");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "share", "Transmux", "bin");
    }

    // ── Platform installers ────────────────────────────────────────────────────

    private static async Task<bool> TryInstallWhisperAsync(IProgress<string>? progress)
    {
        if (OperatingSystem.IsLinux())
            return await TryInstallLinuxAsync(progress);
        if (OperatingSystem.IsMacOS())
            return await TryInstallMacAsync(progress);
        if (OperatingSystem.IsWindows())
            return await TryInstallWindowsAsync(progress);
        return false;
    }

    private static async Task<bool> TryInstallLinuxAsync(IProgress<string>? progress)
    {
        if (await RunCommandAsync("snap", "install whisper-cpp", progress))
            return true;

        if (await RunCommandAsync("brew", "install whisper-cpp", progress))
            return true;

        progress?.Report("Trying prebuilt binary from GitHub...");
        if (await TryDownloadPrebuiltAsync(progress))
            return true;

        progress?.Report("Building whisper.cpp from source (requires git + cmake + C++ compiler)...");
        if (await TryBuildFromSourceAsync(progress))
            return true;

        // Final fallback: Python openai-whisper
        progress?.Report("Trying Python openai-whisper (pip install)...");
        return await TryInstallPythonWhisperAsync(progress);
    }

    private static async Task<bool> TryInstallMacAsync(IProgress<string>? progress)
    {
        if (await RunCommandAsync("brew", "install whisper-cpp", progress))
            return true;

        progress?.Report("Trying prebuilt binary from GitHub...");
        if (await TryDownloadPrebuiltAsync(progress))
            return true;

        progress?.Report("Building whisper.cpp from source...");
        if (await TryBuildFromSourceAsync(progress))
            return true;

        progress?.Report("Trying Python openai-whisper (pip install)...");
        return await TryInstallPythonWhisperAsync(progress);
    }

    private static async Task<bool> TryInstallWindowsAsync(IProgress<string>? progress)
    {
        if (await RunCommandAsync("choco", "install whisper-cpp -y", progress))
            return true;

        if (await RunCommandAsync("scoop", "install whisper-cpp", progress))
            return true;

        progress?.Report("Trying prebuilt binary from GitHub...");
        if (await TryDownloadPrebuiltAsync(progress))
            return true;

        progress?.Report("Building whisper.cpp from source (requires git + cmake + MSVC or MinGW)...");
        if (await TryBuildFromSourceAsync(progress))
            return true;

        progress?.Report("Trying Python openai-whisper (pip install)...");
        return await TryInstallPythonWhisperAsync(progress);
    }

    // ── Python whisper install ─────────────────────────────────────────────────

    private static async Task<bool> TryInstallPythonWhisperAsync(IProgress<string>? progress)
    {
        // Try pip3 first, then pip
        foreach (var pip in new[] { "pip3", "pip" })
        {
            progress?.Report($"Running {pip} install openai-whisper...");
            if (await RunCommandAsync(pip, "install openai-whisper", progress))
            {
                if (TryResolvePythonWhisperBinary() is not null)
                    return true;
            }
        }

        return false;
    }

    // ── GitHub prebuilt download ───────────────────────────────────────────────

    private static async Task<bool> TryDownloadPrebuiltAsync(IProgress<string>? progress)
    {
        try
        {
            progress?.Report("Fetching latest whisper.cpp release info...");
            var asset = await FindReleaseAssetAsync();
            if (asset is null)
            {
                progress?.Report("No matching prebuilt asset found for this platform.");
                return false;
            }

            var (assetName, downloadUrl) = asset.Value;
            progress?.Report($"Downloading {assetName}...");

            var tempDir = Path.Combine(Path.GetTempPath(), "whisper-dl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var archivePath = Path.Combine(tempDir, assetName);
                using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
                {
                    request.Headers.UserAgent.ParseAdd("Transmux/1.0");
                    using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    if (!response.IsSuccessStatusCode)
                    {
                        progress?.Report($"Download failed: {response.StatusCode}");
                        return false;
                    }

                    using var file = File.Create(archivePath);
                    await response.Content.CopyToAsync(file);
                }

                progress?.Report("Extracting...");
                var extractDir = Path.Combine(tempDir, "extracted");
                Directory.CreateDirectory(extractDir);

                if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ZipFile.ExtractToDirectory(archivePath, extractDir, overwriteFiles: true);
                }
                else if (assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                         assetName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
                {
                    await RunShellAsync($"tar xzf {Q(archivePath)} -C {Q(extractDir)}", progress);
                }

                var binaryName = OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli";
                var binary = FindFileRecursive(extractDir, binaryName)
                          ?? FindFileRecursive(extractDir, OperatingSystem.IsWindows() ? "main.exe" : "main");

                if (binary is null)
                {
                    progress?.Report("Could not find whisper-cli in the downloaded archive.");
                    return false;
                }

                return InstallBinary(binary, progress);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Prebuilt download failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<(string name, string url)?> FindReleaseAssetAsync()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/repos/ggerganov/whisper.cpp/releases/latest");
        request.Headers.UserAgent.ParseAdd("Transmux/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("assets", out var assets))
            return null;

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";

            if (MatchesPlatform(name))
                return (name, url);
        }

        return null;
    }

    private static bool MatchesPlatform(string assetName)
    {
        var lower = assetName.ToLowerInvariant();

        if (!lower.EndsWith(".zip") && !lower.EndsWith(".tar.gz") && !lower.EndsWith(".tgz"))
            return false;

        var arch = RuntimeInformation.ProcessArchitecture;
        string[] archKeywords = arch switch
        {
            Architecture.Arm64 => ["arm64", "aarch64"],
            _ => ["x64", "amd64", "x86_64"]
        };

        if (OperatingSystem.IsWindows())
            return (lower.Contains("win") || lower.Contains("windows")) &&
                   archKeywords.Any(lower.Contains);

        if (OperatingSystem.IsMacOS())
            return (lower.Contains("mac") || lower.Contains("darwin")) &&
                   archKeywords.Any(lower.Contains);

        // Linux: require "linux" + arch keyword; fall back to just "linux" if no arch match
        if (lower.Contains("linux") && archKeywords.Any(lower.Contains))
            return true;

        return lower.Contains("linux") && !lower.Contains("win") && !lower.Contains("mac");
    }

    // ── Build from source ──────────────────────────────────────────────────────

    private static async Task<bool> TryBuildFromSourceAsync(IProgress<string>? progress)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "whisper-build-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            var sourceDir = Path.Combine(tempDir, "whisper.cpp");
            var buildDir = Path.Combine(tempDir, "build");

            progress?.Report("Cloning whisper.cpp...");
            await RunShellAsync(
                $"git clone --depth 1 https://github.com/ggerganov/whisper.cpp.git {Q(sourceDir)}",
                progress);

            if (!Directory.Exists(sourceDir))
            {
                progress?.Report("Clone failed — is git installed?");
                return false;
            }

            var cores = Math.Max(1, Environment.ProcessorCount - 1);

            progress?.Report("Configuring with cmake...");
            await RunShellAsync(
                $"cmake -S {Q(sourceDir)} -B {Q(buildDir)} -DCMAKE_BUILD_TYPE=Release",
                progress);

            progress?.Report("Building (this may take a few minutes)...");
            await RunShellAsync(
                $"cmake --build {Q(buildDir)} --config Release --parallel {cores}",
                progress);

            var binaryName = OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli";
            var binary = FindFileRecursive(buildDir, binaryName)
                      ?? FindFileRecursive(buildDir, OperatingSystem.IsWindows() ? "main.exe" : "main");

            if (binary is null)
            {
                progress?.Report("Build finished but whisper-cli binary was not found. Is cmake installed?");
                return false;
            }

            return InstallBinary(binary, progress);
        }
        catch (Exception ex)
        {
            progress?.Report($"Build failed: {ex.Message}");
            return false;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    // ── Install helper ─────────────────────────────────────────────────────────

    private static bool InstallBinary(string sourcePath, IProgress<string>? progress)
    {
        try
        {
            var installDir = GetAppBinDir();
            Directory.CreateDirectory(installDir);

            var targetName = OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli";
            var targetPath = Path.Combine(installDir, targetName);

            File.Copy(sourcePath, targetPath, overwrite: true);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(targetPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            progress?.Report($"Installed whisper-cli to {targetPath}");
            return true;
        }
        catch (Exception ex)
        {
            progress?.Report($"Could not install binary: {ex.Message}");
            return false;
        }
    }

    // ── Utilities ──────────────────────────────────────────────────────────────

    private static string? FindFileRecursive(string directory, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories)
                            .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static Task<bool> RunCommandAsync(string command, string arguments, IProgress<string>? progress) =>
        Task.Run(() =>
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.OutputDataReceived += (_, e) => { if (e.Data is not null) progress?.Report(e.Data); };
                process.ErrorDataReceived += (_, e) => { if (e.Data is not null) progress?.Report(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit(TimeSpan.FromMinutes(10));

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        });

    private static Task RunShellAsync(string command, IProgress<string>? progress)
    {
        var (shell, flag) = OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c")
            : ("/bin/bash", "-c");

        return RunCommandAsync(shell, $"{flag} {Q(command)}", progress)
               .ContinueWith(_ => { });
    }

    private static string Q(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
