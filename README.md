# Transmux

Transmux is a clean desktop audio/video converter powered by Lumyn. Drop in a media file, inspect its format and streams, pick an output format, and convert — all without touching a command line.

## Features

- **Open files easily** — File picker or drag-and-drop support for quick access.
- **Smart format detection** — Automatically suggests the best output format based on input codec (e.g., H.264+AAC → MP4, VP9+Opus → WebM).
- **Auto-skip conversion** — Detects when source already matches target format and confirms before re-encoding.
- **Wide format support** — Convert to MP4 (H.264), WebM, MKV (stream copy), AVI, MOV, MP3, AAC, FLAC, OGG, WAV, or Opus.
- **Flexible subtitle handling** — Include subtitles in output, extract to `.srt` or `.ass`, or strip entirely. Select multiple subtitle tracks.
- **Multi-track audio selection** — Choose which audio tracks to include in the conversion.
- **Fast & Full re-encode modes** — Stream copy (instant, no re-encoding) or full re-encode (quality control). Toggle with Spacebar.
- **Live conversion progress** — Real-time speed and ETA feedback. Cancel anytime.
- **Conversion history** — Track all conversions with timestamps. Quickly open output folders from history.
- **Keyboard shortcuts** — Power-user shortcuts (Ctrl+O: open, Enter: convert, Spacebar: toggle mode, ?: shortcuts help).
- **Session persistence** — Remembers last output directory and format between sessions.
- **Quick folder access** — Open output folder directly after conversion completes.

## Tech Stack

- .NET 10
- C#
- Avalonia UI
- FFmpeg / FFprobe for conversion and media inspection

## Supported OS

- Ubuntu Linux, amd64 (requires `ffmpeg` installed via `apt`)
- Windows, x64 (FFmpeg bundled)
- macOS, Apple Silicon and Intel (FFmpeg bundled)

On Linux, install FFmpeg via your package manager before running Transmux:

```bash
sudo apt install ffmpeg
```

On Windows and macOS, FFmpeg is included in the installer — no separate install needed.

## Installation

### Linux (Ubuntu/Debian)

**Via Ubuntu PPA** (recommended, auto-updates):
```bash
sudo add-apt-repository ppa:piyushdoorwar/transmux
sudo apt update
sudo apt install transmux
```

**Via Debian Repository** (GitHub Pages):
```bash
curl -fsSL https://piyushdoorwar.github.io/transmux/debian/transmux.asc | gpg --dearmor | sudo tee /etc/apt/keyrings/transmux.gpg > /dev/null
echo "deb [signed-by=/etc/apt/keyrings/transmux.gpg] https://piyushdoorwar.github.io/transmux/debian stable main" | sudo tee /etc/apt/sources.list.d/transmux.list
sudo apt update
sudo apt install transmux
```

**Direct `.deb` file**:
```bash
sudo apt install https://github.com/piyushdoorwar/transmux/releases/download/v1.1.0/transmux_1.1.0_amd64.deb
```

### Windows

Download and run the `.exe` installer from [releases](https://github.com/piyushdoorwar/transmux/releases/latest).

### macOS

Download the `.dmg` from [releases](https://github.com/piyushdoorwar/transmux/releases/latest), open it, and drag Transmux to your Applications folder.

## Run Locally

```bash
dotnet restore Transmux.sln
dotnet build Transmux.sln
dotnet run --project src/Transmux.App/Transmux.App.csproj
```

## License

Transmux is source available, not open source under an OSI-approved license.
Personal, non-commercial use of official releases is permitted. You may view
the source code, build it for personal evaluation, and submit issues or pull
requests to the official repository.

Copying, redistribution, republishing, modification for distribution,
commercial use, resale, hosting as a service, and use in commercial products
or services are not permitted without explicit written permission from the
copyright holder.

## Build Packages

Build the Ubuntu `.deb`:

```bash
./scripts/build-linux.sh
```

Build the Windows installer from Windows PowerShell:

```powershell
./scripts/build-windows.ps1
```

Build the unsigned macOS `.app` zip from macOS:

```bash
RID=osx-arm64 ./scripts/build-macos.sh
RID=osx-x64 ./scripts/build-macos.sh
```

GitHub Actions also builds release artifacts through the `Build release artifacts` workflow.

Package versions use the base version in `VERSION` plus the GitHub Actions run number, for example `0.1.42`. Set `VERSION` in the environment to override the full package version manually.
