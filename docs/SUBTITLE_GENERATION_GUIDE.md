# Subtitle Generation - Usage Guide

## How It Works

When you select "Subs generation" mode and click "Generate subtitles":

1. **Setup Phase**
   - App checks if `whisper-cli` is installed
   - If not found, automatically downloads and installs it (platform-specific)
   - Checks if the selected model is cached locally
   - If not, downloads the model file from Hugging Face (~40MB to 3GB depending on model)

2. **Generation Phase**
   - Extracts audio from your input file (16-bit WAV format)
   - Passes audio to whisper for transcription
   - Generates SRT subtitle file in your output location

## What Gets Installed

### whisper.cpp
A C++ implementation of OpenAI's Whisper, optimized for local inference without GPUs or external APIs.
- Installed to: `~/.local/share/Transmux/bin/whisper-cli` (Linux/macOS)
- Or: `%LOCALAPPDATA%\Transmux\bin\whisper-cli.exe` (Windows)

### Models
Downloaded on-demand and cached locally:
- **tiny** (40MB) - Very fast, basic accuracy
- **base** (140MB) - Fast, good balance
- **small** (500MB) - Better accuracy
- **medium** (1.5GB) - High accuracy
- **large-v3-turbo** (3GB) - Highest quality

Location: `~/.local/share/Transmux/whisper/models/` (Linux/macOS)

## First Run

Your first subtitle generation will take longer due to:
1. Installing whisper.cpp (~5-10 minutes on slow connection)
2. Downloading the selected model (~5-30 minutes depending on model and connection)
3. Processing your audio

Subsequent runs will be much faster since both tools and models are cached.

## Requirements

- **Disk Space**: 40MB - 3GB+ depending on model choice
- **Internet**: Required for first-time setup and model download only
- **CPU**: Multi-core recommended (will use ~50% of available cores)
- **Memory**: 2GB minimum, 8GB+ recommended for larger models

## Troubleshooting

### Installation Fails
If automatic installation fails:
1. **Linux**: `sudo apt-get install build-essential cmake && git clone https://github.com/ggerganov/whisper.cpp.git && cd whisper.cpp && make && sudo make install`
2. **macOS**: `brew install whisper-cpp`
3. **Windows**: Download from https://github.com/ggerganov/whisper.cpp/releases

### Slow Performance
- Model selection affects speed (try "Very fast" or "Fast")
- Larger models are more accurate but slower
- Multi-core systems will perform better
- Check CPU/memory availability

### Canceled Operations
Press "Cancel" anytime to stop processing. Already-downloaded models are preserved for future use.
