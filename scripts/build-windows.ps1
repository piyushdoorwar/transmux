# Build the Transmux Windows installer.
# Requires: dotnet-sdk-10.0, Inno Setup (ISCC.exe on PATH or in default install location)
# ffmpeg is downloaded automatically from BtbN/FFmpeg-Builds (essentials build).
param(
    [string]$Configuration = $env:CONFIGURATION,
    [string]$Rid           = $env:RID,
    [string]$Version       = $env:VERSION,
    [string]$FfmpegBinDir  = $env:FFMPEG_BIN_DIR
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Configuration)) { $Configuration = "Release" }
if ([string]::IsNullOrWhiteSpace($Rid))           { $Rid = "win-x64" }
if ([string]::IsNullOrWhiteSpace($Version))       { $Version = "0.0.0-dev" }

if ($Rid -ne "win-x64") {
    throw "Unsupported RID: $Rid. Only win-x64 is supported."
}

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot    = Resolve-Path (Join-Path $scriptDir "..")
Set-Location $repoRoot

$appProject   = "src/Transmux.App/Transmux.App.csproj"
$publishDir   = Join-Path $repoRoot "artifacts\publish\$Rid"
$packageRoot  = Join-Path $repoRoot "artifacts\pkg\transmux-windows\Transmux"
$packageOutDir = Join-Path $repoRoot "artifacts\packages"

# ── Resolve ffmpeg ────────────────────────────────────────────────────────────

function Resolve-FfmpegDirectory {
    param([string]$ProvidedDir)

    if (-not [string]::IsNullOrWhiteSpace($ProvidedDir)) {
        $resolved = Resolve-Path $ProvidedDir
        $exe = Get-ChildItem -Path $resolved -Recurse -File -Filter "ffmpeg.exe" | Select-Object -First 1
        if ($null -eq $exe) { throw "FFMPEG_BIN_DIR does not contain ffmpeg.exe: $ProvidedDir" }
        return $resolved.Path
    }

    Write-Host "Fetching latest ffmpeg essentials release from BtbN/FFmpeg-Builds..."
    $apiUrl  = "https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/latest"
    $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ "User-Agent" = "Transmux-Packager" }

    $asset = $release.assets |
        Where-Object { $_.name -match '^ffmpeg-master-latest-win64-gpl\.zip$' } |
        Select-Object -First 1

    if ($null -eq $asset) {
        throw "Could not find ffmpeg-master-latest-win64-gpl.zip in BtbN/FFmpeg-Builds latest release."
    }

    $extractDir = Join-Path "artifacts\downloads" "ffmpeg-win64"
    Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $extractDir | Out-Null

    $zipPath = Join-Path $extractDir "ffmpeg.zip"
    Write-Host "Downloading $($asset.browser_download_url)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath `
        -Headers @{ "User-Agent" = "Transmux-Packager" }

    Write-Host "Extracting..."
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    $ffmpegExe = Get-ChildItem -Path $extractDir -Recurse -File -Filter "ffmpeg.exe" | Select-Object -First 1
    if ($null -eq $ffmpegExe) { throw "ffmpeg.exe not found after extracting archive." }

    return $ffmpegExe.DirectoryName
}

# ── Build ─────────────────────────────────────────────────────────────────────

Write-Host "Building Transmux $Version ($Rid)..."
dotnet restore Transmux.sln
dotnet build Transmux.sln -c $Configuration --no-restore
dotnet publish $appProject -c $Configuration -r $Rid --self-contained true `
    -o $publishDir -p:Version=$Version -p:InformationalVersion=$Version

# ── Stage package layout ──────────────────────────────────────────────────────

if (Test-Path $packageRoot) { Remove-Item -Recurse -Force $packageRoot }
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Force -Path $packageOutDir | Out-Null

# Copy published app
Copy-Item -Path (Join-Path $publishDir "*") -Destination $packageRoot -Recurse -Force

# Resolve and copy ffmpeg + ffprobe
$ffmpegDir = Resolve-FfmpegDirectory -ProvidedDir $FfmpegBinDir
foreach ($bin in @("ffmpeg.exe", "ffprobe.exe")) {
    $src = Join-Path $ffmpegDir $bin
    if (Test-Path $src) {
        Copy-Item $src (Join-Path $packageRoot $bin) -Force
        Write-Host "Bundled $bin"
    } else {
        Write-Warning "$bin not found in $ffmpegDir — skipping."
    }
}

# ── InnoSetup ─────────────────────────────────────────────────────────────────

$issFile = Join-Path $repoRoot "packaging\windows\transmux.iss"

$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $iscc = @{ Source = $c }; break }
    }
}
if ($null -eq $iscc) {
    throw "ISCC.exe not found. Install Inno Setup 6 or add it to PATH."
}

Write-Host "Running Inno Setup..."
& $iscc.Source `
    "/DAppVersion=$Version" `
    "/DSourceDir=$packageRoot" `
    "/DRepoRoot=$repoRoot" `
    $issFile

Write-Host "Windows artifacts:"
Get-ChildItem -Path $packageOutDir -Filter "*.exe" | Select-Object -ExpandProperty FullName
