# Transmux (powered by Lumyn) — Agent Reference

> **Usage**: At the start of every session, read this file first. It provides a complete picture of the solution — structure, architecture, features, release pipeline, and conventions — so you don't need to crawl the codebase from scratch.
> After completing any feature work, update the relevant section(s) of this file.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Tech Stack](#2-tech-stack)
3. [Solution Structure](#3-solution-structure)
4. [Architecture](#4-architecture)
5. [Key Source Files](#5-key-source-files)
6. [Features](#6-features)
7. [FFmpeg Integration](#7-ffmpeg-integration)
8. [State & Persistence](#8-state--persistence)
9. [UI Layout & Windows](#9-ui-layout--windows)
10. [Build & Packaging](#10-build--packaging)
11. [CI/CD Workflows](#11-cicd-workflows)
12. [Versioning & Release](#12-versioning--release)
13. [Website / Site](#13-website--site)
14. [Development Setup](#14-development-setup)
15. [Conventions & Patterns](#15-conventions--patterns)

---

## 1. Project Overview

**Transmux** is a clean, minimal desktop audio/video conversion tool powered by FFmpeg, built on .NET 10 + Avalonia UI. It targets Windows x64, Ubuntu Linux amd64/arm64, and macOS (Apple Silicon + Intel).

- **Repo**: `lumyn-transmux`
- **Owner/Author**: Piyush Doorwar
- **Brand**: "Transmux — powered by Lumyn" (reuses Lumyn color palette and visual identity)
- **License**: Source available — non-commercial personal use
- **Current version base**: `1.0`
- **Website**: deployed to GitHub Pages from `/site/`

Design philosophy: a single-purpose, no-frills conversion tool. Pick a file, pick an output format, convert. No hidden complexity.

---

## 2. Tech Stack

| Layer | Technology |
|---|---|
| Language | C# (latest, nullable enabled, implicit usings) |
| Runtime | .NET 10.0 |
| UI Framework | Avalonia UI 11.3.14 |
| UI Theme | Fluent (dark, Lumyn palette) |
| Conversion Engine | FFmpeg (process-based, bundled binary) |
| Media Inspection | FFprobe (bundled alongside FFmpeg) |
| Packaging | dpkg (Linux .deb), Inno Setup (Windows .exe), native .app/.dmg (macOS) |
| Build System | .NET CLI (`dotnet build / publish`) |
| Package Manager | NuGet (centralized via `Directory.Packages.props`) |
| CI/CD | GitHub Actions |
| Deployment | GitHub Releases + GitHub Pages |

**No mpv/libmpv dependency.** Transmux is purely a conversion tool; playback is not a feature.

---

## 3. Solution Structure

```
lumyn-transmux/
├── Lumyn.sln                        # Visual Studio solution (2 projects)
├── Directory.Build.props            # Global build config (net10.0, nullable, etc.)
├── Directory.Packages.props         # Central NuGet version management
│
├── src/
│   ├── Lumyn.App/                   # UI / Presentation layer
│   │   ├── Program.cs               # Entry point
│   │   ├── App.axaml / App.axaml.cs # Application bootstrap, styles, resources
│   │   ├── Assets/
│   │   │   ├── Icons/               # SVG icons + transmux.ico (Lumyn logo)
│   │   │   └── Styles/Lumyn.axaml   # Custom styling (Lumyn dark theme overrides)
│   │   ├── Models/
│   │   │   └── ConversionJob.cs     # Represents a single conversion job (input, output format, subtitle options)
│   │   ├── ViewModels/
│   │   │   └── MainViewModel.cs     # MVVM command routing, UI state, progress tracking
│   │   └── Views/
│   │       ├── MainWindow.axaml / .axaml.cs   # Main single-window UI
│   │       └── AboutDialog.axaml / .axaml.cs  # Credits + version
│   │
│   └── Lumyn.Core/                  # Core business logic (no UI dependency)
│       ├── Models/
│       │   ├── MediaInfo.cs         # Detected format, streams, codec info from ffprobe
│       │   ├── ConversionOptions.cs # Input/output paths, format, codec, subtitle mode
│       │   └── ConversionProgress.cs # Progress report (percent, elapsed, ETA, speed)
│       └── Services/
│           ├── FfmpegService.cs          # FFmpeg/ffprobe process wrapper — core engine
│           ├── MediaInspector.cs         # Runs ffprobe, parses JSON output into MediaInfo
│           └── SettingsService.cs        # JSON persistence (last output dir, preferences)
│
├── scripts/
│   ├── build-linux.sh               # Linux .deb packaging
│   ├── build-windows.ps1            # Windows installer via Inno Setup
│   └── build-macos.sh               # macOS .app + .dmg packaging
│
├── packaging/
│   ├── windows/                     # Inno Setup .iss config (bundles ffmpeg)
│   ├── linux/                       # .deb resources (desktop file, MIME types, postinst for ffmpeg)
│   └── macos/                       # macOS packaging resources
│
├── artifacts/                       # Build output (gitignored)
│
├── site/                            # Static website (GitHub Pages)
│
└── .github/
    ├── agents.md                    # THIS FILE
    └── workflows/
        ├── build-artifacts.yml      # Build all platforms on push to main
        ├── release.yml              # Tag-triggered release with GitHub Release artifacts
        └── static.yml               # Deploy /site to GitHub Pages
```

### NuGet Dependencies (`Directory.Packages.props`)

```
Avalonia             11.3.14
Avalonia.Desktop     11.3.14
Avalonia.Themes.Fluent  11.3.14
Avalonia.Fonts.Inter    11.3.14
```

`Lumyn.Core` has **no NuGet dependencies** beyond the base runtime. All FFmpeg interaction is via `Process`.
│       │   ├── VideoFrameData.cs    # Frame data for OpenGL rendering
│       │   └── SubtitleSearchResult.cs
│       └── Services/
│           ├── PlaybackService.cs        # mpv wrapper, 995 lines — main engine
│           ├── SettingsService.cs        # JSON persistence, 257 lines
│           ├── ChromecastCastService.cs  # Chromecast (Google Cast v2) discovery, HTTP file server, cast control
│           ├── SubtitleParser.cs         # SRT/ASS/SSA/VTT parser
│           └── SubtitleSearchService.cs  # Online subtitle search
│
├── scripts/
│   ├── build-linux.sh               # Linux .deb packaging (266 lines)
│   ├── build-windows.ps1            # Windows installer via Inno Setup (363 lines, PowerShell)
│   ├── build-macos.sh               # macOS .app + .dmg packaging
│   └── build-linux-flatpak.sh       # Flatpak packaging (alternate format)
│
├── packaging/
│   ├── windows/                     # Inno Setup .iss config
│   └── linux/                       # Linux packaging resources (desktop file, MIME types)
│
├── artifacts/                       # Build output (gitignored)
│   ├── packages/                    # Final distributable packages
│   ├── publish/                     # Intermediate dotnet publish output
│   └── pkg/                         # Packaged app structures
│
├── site/                            # Static website (GitHub Pages)
│
└── .github/
    ├── agents.md                    # THIS FILE
    └── workflows/
        ├── build-artifacts.yml      # Build all platforms on push to main
        ├── release.yml              # Tag-triggered release with GitHub Release artifacts
        └── static.yml              # Deploy /site to GitHub Pages
```

### NuGet Dependencies (`Directory.Packages.props`)

```
Avalonia             11.3.14
Avalonia.Desktop     11.3.14
Avalonia.Themes.Fluent  11.3.14
Avalonia.Fonts.Inter    11.3.14
```

`Lumyn.Core` NuGet dependencies:
```
GoogleCast           1.7.0   (Google Cast v2 protocol — includes Zeroconf for mDNS and protobuf-net)
```

---

## 4. Architecture

Pattern: **MVVM + Service Layer**, single process, single window.

```
┌─────────────────────────────────────────────────────┐
│  Views (Avalonia XAML)                              │
│  MainWindow + AboutDialog                           │
└───────────────────┬─────────────────────────────────┘
                    │ Data binding (INPC)
┌───────────────────▼─────────────────────────────────┐
│  MainViewModel                                      │
│  - RelayCommand pattern                             │
│  - UI state (InputFile, OutputFormat, Progress)     │
│  - Drives conversion job lifecycle                  │
│  - Dispatcher.UIThread for all UI updates           │
└───────────────────┬─────────────────────────────────┘
                    │ Direct method calls + events
┌───────────────────▼─────────────────────────────────┐
│  Lumyn.Core Services                                │
│                                                     │
│  FfmpegService                                      │
│  ├─ Launches ffmpeg as a child Process              │
│  ├─ Parses stderr progress lines (time=, speed=)    │
│  ├─ Reports ConversionProgress via event/callback   │
│  ├─ Cancellation via Process.Kill                   │
│  └─ Builds ffmpeg argument string from options      │
│                                                     │
│  MediaInspector                                     │
│  ├─ Launches ffprobe -v quiet -print_format json    │
│  ├─ Parses JSON output into MediaInfo               │
│  └─ Exposes: container, duration, streams           │
│     (codec, type, language, bitrate, resolution)    │
│                                                     │
│  SettingsService                                    │
│  ├─ ~/.config/Transmux/settings.json (Linux)        │
│  └─ Persists: last output directory, preferences    │
└───────────────────┬─────────────────────────────────┘
                    │ Process spawn
┌───────────────────▼─────────────────────────────────┐
│  FFmpeg + FFprobe (bundled binaries)                │
│  - All audio/video format support                   │
│  - Hardware-accelerated encoding (optional)         │
│  - Subtitle extraction (SRT, ASS, WebVTT)           │
└─────────────────────────────────────────────────────┘
```

### Startup Sequence

1. `Program.cs` → configures Avalonia with X11/Wayland options
2. `App.axaml.cs` → creates `FfmpegService`, `MediaInspector`, `SettingsService`
3. Creates `MainViewModel` injecting all services
4. Creates `MainWindow` with `ViewModel` as `DataContext`
5. Optional: command-line file argument pre-fills the input file

### Conversion Flow

1. User selects input file → `MainViewModel.SelectInputFileAsync()`
2. `MediaInspector.InspectAsync(filePath)` → spawns ffprobe, returns `MediaInfo`
3. ViewModel populates detected format, streams, codec list in UI
4. User picks output format, subtitle option, output path
5. `MainViewModel.StartConversionAsync()` → builds `ConversionOptions`
6. `FfmpegService.ConvertAsync(options, progressCallback, cancellationToken)`
7. ffmpeg progress parsed from stderr → `ConversionProgress` events update the progress bar
8. On completion: success message shown; output file is ready at the chosen path

---

## 5. Key Source Files

| File | Role |
|---|---|
| `src/Lumyn.Core/Services/FfmpegService.cs` | Core FFmpeg process wrapper — argument building, progress parsing, cancellation |
| `src/Lumyn.Core/Services/MediaInspector.cs` | Runs ffprobe, parses JSON into MediaInfo |
| `src/Lumyn.Core/Services/SettingsService.cs` | JSON persistence: last output directory, preferences |
| `src/Lumyn.Core/Models/MediaInfo.cs` | Detected container, duration, stream list |
| `src/Lumyn.Core/Models/ConversionOptions.cs` | Input/output paths, target format, codec, subtitle mode |
| `src/Lumyn.Core/Models/ConversionProgress.cs` | Progress report (percent, elapsed, ETA, speed) |
| `src/Lumyn.App/ViewModels/MainViewModel.cs` | MVVM hub: commands, UI state, job lifecycle |
| `src/Lumyn.App/Views/MainWindow.axaml` | Full single-window UI layout |
| `src/Lumyn.App/Models/ConversionJob.cs` | UI-layer job descriptor |

---

## 6. Features

### Input
- Open audio or video file via file picker or drag-and-drop
- Detected format displayed: container, duration, video stream (codec, resolution, fps), audio stream(s) (codec, channel count, sample rate), subtitle tracks
- Supports all formats that FFmpeg can demux (MKV, MP4, AVI, MOV, WebM, FLV, TS, MP3, AAC, FLAC, OGG, WAV, OPUS, M4A, etc.)

### Output Format Selection
- Choose target container/format from a curated list:
  - **Video**: MP4 (H.264+AAC), WebM (VP9+Opus), MKV (copy), AVI, MOV
  - **Audio-only**: MP3, AAC, FLAC, OGG, WAV, OPUS, M4A
- Format list is aware of the input: audio-only formats are promoted when input has no video stream

### Subtitle Handling
- If the input file contains subtitle tracks:
  - **Include subtitles**: embed them in the output (for containers that support it, e.g. MKV, MP4)
  - **Extract subtitles**: save subtitle tracks to a separate `.srt` or `.ass` file alongside the output
  - **No subtitles**: strip all subtitle tracks
- Subtitle mode selector is shown/hidden based on whether the input has subtitle tracks

### Output Path
- User selects output directory and filename before starting conversion
- Output directory defaults to the same folder as the input file (persisted as last-used dir)
- Output filename defaults to `{inputName}.{ext}` with the chosen format's extension

### Conversion Progress
- Progress bar (0–100%) driven by ffmpeg's `time=` output relative to input duration
- Speed indicator (e.g. `2.4×`) and ETA
- Cancel button aborts the ffmpeg process immediately
- On completion: brief success state with "Open folder" shortcut

### UI & Platform
- Clean dark theme (Lumyn palette: `#111111` background, `#DEDAD5` text, `#3A9B4B` accent)
- Single window, no sidebar — focused workflow
- Drag-and-drop input file onto the window
- About dialog (version + credits)
- "Powered by Lumyn" branding in footer

---

## 7. FFmpeg Integration

All FFmpeg and FFprobe interaction is via `System.Diagnostics.Process` — no native P/Invoke.

### FfmpegService

Builds the ffmpeg command from `ConversionOptions`:

```
ffmpeg -y -i "{inputPath}" [{encodingArgs}] [{subtitleArgs}] "{outputPath}"
```

**Encoding argument mapping:**

| Output Format | Video Codec | Audio Codec | Container |
|---|---|---|---|
| MP4 (H.264) | `libx264 -crf 23 -preset fast` | `aac -b:a 192k` | `.mp4` |
| WebM (VP9) | `libvpx-vp9 -crf 33 -b:v 0` | `libopus -b:a 128k` | `.webm` |
| MKV (copy) | `copy` | `copy` | `.mkv` |
| AVI | `libx264` | `mp3` | `.avi` |
| MOV | `libx264` | `aac` | `.mov` |
| MP3 (audio) | — | `libmp3lame -q:a 2` | `.mp3` |
| AAC (audio) | — | `aac -b:a 256k` | `.m4a` |
| FLAC (audio) | — | `flac` | `.flac` |
| OGG (audio) | — | `libvorbis -q:a 6` | `.ogg` |
| WAV (audio) | — | `pcm_s16le` | `.wav` |
| OPUS (audio) | — | `libopus -b:a 128k` | `.opus` |

**Subtitle argument mapping:**

| Mode | ffmpeg args |
|---|---|
| Include (embed) | `-c:s mov_text` (MP4) or `-c:s copy` (MKV) |
| Extract to SRT | `-map 0:s:0 "{outputPath}.srt"` |
| Extract to ASS | `-map 0:s:0 "{outputPath}.ass"` |
| None | `-sn` |

**Progress parsing:**  
ffmpeg writes progress to stderr as: `frame=  120 fps= 25 q=-1.0 size=  1024kB time=00:00:04.80 bitrate=1747.1kbits/s speed=2.4x`  
`FfmpegService` reads stderr line-by-line, extracts `time=` value, divides by input duration → percent complete.

### MediaInspector

Runs:
```
ffprobe -v quiet -print_format json -show_format -show_streams "{inputPath}"
```
Parses the JSON into `MediaInfo` (format name, duration in seconds, list of `StreamInfo` with codec type, codec name, language, width/height/fps for video, channel count/sample rate for audio).

### FFmpeg Binary Location

- **Linux .deb**: ffmpeg installed as system dependency (`Depends: ffmpeg` in control file). `FfmpegService` uses `"ffmpeg"` / `"ffprobe"` directly on PATH.
- **Windows**: ffmpeg binaries bundled in the install directory. Build script downloads `ffmpeg-release-essentials.zip` from BtbN/FFmpeg-Builds. `FfmpegService` checks `AppContext.BaseDirectory` first, then PATH.
- **macOS**: ffmpeg bundled inside `.app/Contents/MacOS/`. Build script installs via Homebrew and copies binaries.

---

## 8. State & Persistence

### In-Memory State (ViewModel)

```csharp
// Active job
string? InputFilePath
MediaInfo? DetectedMedia
string? OutputFilePath
OutputFormat SelectedFormat
SubtitleMode SelectedSubtitleMode

// Progress
bool IsConverting
double ProgressPercent    // 0.0 – 100.0
string SpeedText          // e.g. "2.4×"
string EtaText            // e.g. "00:23"
bool IsComplete
string? ErrorMessage
CancellationTokenSource? _cts
```

### Settings Persistence

- **Location**: `~/.config/Transmux/settings.json` (Linux); `%AppData%\Transmux\settings.json` (Windows); `~/Library/Application Support/Transmux/settings.json` (macOS)
- **Contents**:
  - `LastOutputDirectory` — last directory the user saved to
  - `LastOutputFormat` — last selected output format name

---

## 9. UI Layout & Windows

### MainWindow
- **Default size**: 680×520; **Minimum**: 500×420
- **Decorations**: `BorderOnly` (custom title bar, Lumyn style)
- **Background**: `#111111`; **Foreground**: `#DEDAD5`
- **Theme**: Fluent dark + `Lumyn.axaml` overrides

```
┌─────────────────────────────────────────────┐  ← TopBar (38px)
│  [Transmux logo]   Title        [About] [✕] │
├─────────────────────────────────────────────┤
│  ┌───────────────────────────────────────┐  │
│  │  Drop a file or click to browse       │  │  ← Drop zone / file picker
│  └───────────────────────────────────────┘  │
│                                             │
│  Detected:  MKV · H.264 · 1920×1080        │  ← MediaInfo panel (hidden until file loaded)
│             AAC 2ch · Subtitles: 2 tracks  │
│                                             │
│  Output format:  [ MP4 (H.264) ▾ ]         │  ← Format selector
│  Subtitles:      [ Extract to SRT ▾ ]      │  ← Subtitle mode (shown if input has subs)
│  Save to:        [ /home/user/output.mp4 ] │  ← Output path picker
│                                             │
│  [ Convert ]                               │  ← Primary action
│                                             │
│  ████████████████░░░░░  64%  2.4×  ~00:12  │  ← Progress bar (hidden until converting)
│  [ Cancel ]                                 │
│                                             │
│  ✓ Done — output.mp4                [ Open folder ] │  ← Completion state
└─────────────────────────────────────────────┘
│  Powered by Lumyn                           │  ← Footer
```

### Visual States

| State | Visible elements |
|---|---|
| **Idle** | Drop zone, empty format selector (disabled), Convert button (disabled) |
| **File loaded** | Drop zone (shows filename), MediaInfo panel, Format selector, Subtitle mode (if applicable), Save-to path, Convert button (enabled) |
| **Converting** | Progress bar, speed + ETA, Cancel button; input controls disabled |
| **Complete** | Success message + "Open folder" link; ready for next file |
| **Error** | Error message inline; ready to retry |

### Dialogs

| Dialog | Purpose |
|---|---|
| `AboutDialog` | Version + credits + "Powered by Lumyn" |

---

## 10. Build & Packaging

### Local Dev

```bash
dotnet restore Lumyn.sln
dotnet build Lumyn.sln
dotnet run --project src/Lumyn.App/Lumyn.App.csproj
```

FFmpeg must be on PATH for local development.

### Linux — `.deb` package (`scripts/build-linux.sh`)

1. `dotnet publish -c Release -r linux-x64 --self-contained true`
2. Build `.deb` structure:
   - `/opt/transmux/` — binaries
   - `/usr/bin/transmux` — symlink to launcher script
   - `/usr/share/applications/transmux.desktop`
   - `/usr/share/icons/`
   - `DEBIAN/control` with `Depends: ffmpeg`
3. `dpkg-deb` → `transmux_X.X.X_amd64.deb`

**ffmpeg strategy (Linux)**: listed as a package dependency (`Depends: ffmpeg`). No bundling — the system ffmpeg is used. `FfmpegService` resolves via PATH.

**Supports**: `amd64` and `arm64`.

### Windows — `.exe` installer (`scripts/build-windows.ps1`)

1. `dotnet publish -c Release -r win-x64 --self-contained true`
2. Download `ffmpeg-release-essentials.zip` from BtbN/FFmpeg-Builds (or use `FFMPEG_BIN_DIR` env var)
3. Extract `ffmpeg.exe` + `ffprobe.exe` into the staging directory
4. Compile Inno Setup `.iss` → `transmux_X.X.X_win-x64_setup.exe`

**Inno Setup** (`packaging/windows/transmux.iss`): Bundles `ffmpeg.exe` and `ffprobe.exe` alongside the app. No separate ffmpeg install step needed for the end user.

### macOS — `.dmg` bundle (`scripts/build-macos.sh`)

1. `dotnet publish -c Release -r osx-arm64 (or osx-x64) --self-contained true`
2. `brew install ffmpeg` to get binaries
3. Copy `ffmpeg` + `ffprobe` into `.app/Contents/MacOS/`
4. Code-sign ad-hoc, create `.dmg` via `hdiutil`

**No flatpak support.** Linux distribution is exclusively via `.deb`.

---

## 11. CI/CD Workflows

### `build-artifacts.yml` — triggered on push to `main` or manual dispatch

| Job | Runner | Output artifact |
|---|---|---|
| `linux-deb` | ubuntu-latest | `transmux-linux-amd64-deb` (*.deb) |
| `windows-installer` | windows-latest | `transmux-windows-x64-installer` (*_setup.exe) |
| `macos-arm64` | macos-15 | `transmux-macos-osx-arm64` (*.dmg) |
| `macos-x64` | macos-15-intel | `transmux-macos-osx-x64` (*.dmg) |

All jobs install .NET 10.0 SDK.

### `release.yml` — triggered on `v*` tag push or manual dispatch

- Runs same build jobs as `build-artifacts.yml`
- Additional `github-release` job: attaches all artifacts to a GitHub Release

### `static.yml` — triggered on push to `main`

- Deploys `/site/` directory to GitHub Pages

---

## 12. Versioning & Release

- **Version source**: Git tag. Push a tag like `v1.0.0` → `release.yml` fires automatically.
- **Tag format**: `v{MAJOR}.{MINOR}.{PATCH}` for production, `v{MAJOR}.{MINOR}.{PATCH}-{label}` for pre-release.
- **How the version flows**:
  1. `release.yml` extracts version from the tag (`v1.0.0` → `1.0.0`) in a `prepare` job
  2. All build jobs receive it as the `VERSION` env var
  3. Each build script passes it via `-p:Version=... -p:InformationalVersion=...`
  4. Baked into `AssemblyInformationalVersionAttribute`
  5. `AboutDialog.axaml.cs` reads it at runtime
- **Local dev builds**: Show `0.0.0-dev`
- **Artifact names**: e.g., `transmux_1.0.0_amd64.deb`, `transmux_1.0.0_win-x64_setup.exe`, `transmux_1.0.0_macos-arm64.dmg`

### Release checklist

1. Merge everything to `main`
2. Push a tag: `git tag v1.0.0 && git push origin v1.0.0`
3. `release.yml` fires — builds all platforms, creates GitHub Release

---

## 13. Website / Site

- Located at `/site/` in the repo
- Static HTML/CSS site, reusing Lumyn visual design
- Deployed automatically to GitHub Pages via `static.yml` on every push to `main`
- URL: `https://piyushdoorwar.github.io/lumyn-transmux/`
- Contains: landing page (what it does, download section), releases page, privacy/policy page

### Landing page (`/site/index.html`)

- Hero section: tool description, "Convert any audio/video" tagline
- Feature highlights: FFmpeg-powered, subtitle extraction, simple format picker, cross-platform
- Download section: per-platform buttons (Linux .deb / Windows .exe / macOS .dmg) populated by `app.js` from GitHub Releases API
- "Powered by Lumyn" footer branding

### Releases page (`/site/releases/`)

- `index.html` — OS filter tabs + stable-only toggle
- `releases.js` — fetches all non-draft releases from GitHub API, renders paginated list with OS-filtered assets

---

## 14. Development Setup

### Ubuntu Linux

```bash
# Install .NET SDK 10.0
# (via Microsoft repo or snap)

# Install ffmpeg
sudo apt install ffmpeg

# Clone and run
git clone ...
cd lumyn-transmux
dotnet run --project src/Lumyn.App/Lumyn.App.csproj
```

### Windows

- Install .NET SDK 10.0
- Download `ffmpeg.exe` + `ffprobe.exe` from BtbN/FFmpeg-Builds and add to PATH (or set `FFMPEG_BIN_DIR`)
- Run via VS or `dotnet run`

### macOS

```bash
brew install dotnet ffmpeg
dotnet run --project src/Lumyn.App/Lumyn.App.csproj
```

---

## 15. Conventions & Patterns

- **MVVM**: Views bind to `MainViewModel` via `DataContext`. No code-behind logic beyond Avalonia event wiring.
- **RelayCommand**: Standard `ICommand` wrapper used throughout `MainViewModel`.
- **Thread safety**: All UI updates via `Dispatcher.UIThread.InvokeAsync`. FFmpeg process stderr is read on a background thread; progress events dispatched to UI thread.
- **Nullable**: Enabled globally. All fields/properties use nullable annotations.
- **No DI container**: Services are concrete classes, injected via constructor. Manual injection in `App.axaml.cs`.
- **No unsafe code**: Unlike the media player origin, Transmux has no P/Invoke or OpenGL interop. `AllowUnsafeBlocks` can be removed.
- **FFmpeg path resolution**: `FfmpegService` checks `AppContext.BaseDirectory` first (for bundled binaries on Windows/macOS), then falls back to PATH resolution. Fail-fast with a clear error message if ffmpeg is not found.
- **Settings path**: `Environment.GetFolderPath(SpecialFolder.ApplicationData)` + `Transmux/settings.json`.
- **Avalonia resources**: Icons and styles defined in `App.axaml` as `Application.Resources`. `Lumyn.axaml` style sheet reused wholesale for branding consistency.
- **Output format enum**: `OutputFormat` is a C# enum with an associated metadata record (`FormatInfo`) carrying the display name, file extension, and ffmpeg argument fragments. New formats are added by extending this record list — no scattered switch statements.

---

## Changelog (Feature Updates)

> Update this section whenever a feature is added, removed, or significantly changed.

- **v1.0** — Initial release: file open, ffprobe media inspection, output format selection, subtitle mode (embed/extract/none), output path picker, ffmpeg conversion with progress, cancel, Windows/Linux/macOS packaging.

| Date | Change |
|---|---|

