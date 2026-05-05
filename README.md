# Transmux

Transmux is a clean desktop audio/video converter powered by Lumyn. Drop in a media file, inspect its format and streams, pick an output format, and convert — all without touching a command line.

## Features

- Open audio and video files via file picker or drag and drop.
- Inspect detected format, video stream, audio streams, and subtitle tracks.
- Convert to MP4 (H.264), WebM, MKV (stream copy), AVI, MOV, MP3, AAC, FLAC, OGG, WAV, or Opus.
- Include subtitles in the output, extract them to a separate `.srt` or `.ass` file, or strip them.
- Live progress bar with speed and ETA.
- Cancel in-flight conversions.
- Remembers last output directory and format between sessions.
- Open output folder directly after conversion completes.

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

**Via Debian Repository** (recommended):
```bash
echo "deb https://piyushdoorwar.github.io/transmux/debian stable main" | sudo tee /etc/apt/sources.list.d/transmux.list
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
