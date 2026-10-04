# Oblivion - one-line installer for Windows 10 / 11.
#
#   irm https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/install.ps1 | iex
#
# Downloads the latest Oblivion setup from GitHub Releases and runs it. Files that
# PowerShell downloads are not marked as "downloaded from the internet", so Microsoft
# Defender SmartScreen does not stop the setup or the installed app.
#
# Optional environment variables (used by CI):
#   OBLIVION_SETUP          path of a local setup to install instead of downloading
#   OBLIVION_SILENT=1       install without the wizard
#   OBLIVION_DOWNLOAD_ONLY  download the setup to this path and stop

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is far faster without its progress bar
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$repo = 'anilg12/Oblivion-Uninstaller'
Write-Host ''
Write-Host '==> Oblivion kuruluyor / installing...' -ForegroundColor Magenta

$setup = $env:OBLIVION_SETUP
$downloaded = $false
if (-not $setup) {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" `
        -Headers @{ 'User-Agent' = 'Oblivion-Installer'; 'Accept' = 'application/vnd.github+json' }
    $asset = $release.assets | Where-Object { $_.name -like '*-Windows-Setup.exe' } | Select-Object -First 1
    if (-not $asset) {
        throw "Kurulum dosyasi bulunamadi / no Windows setup found: https://github.com/$repo/releases"
    }
    $setup = if ($env:OBLIVION_DOWNLOAD_ONLY) { $env:OBLIVION_DOWNLOAD_ONLY } else { Join-Path $env:TEMP $asset.name }
    Write-Host "    $($release.tag_name): $($asset.browser_download_url)"
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $setup -UseBasicParsing
    $downloaded = $true
    if ($env:OBLIVION_DOWNLOAD_ONLY) {
        Write-Host "    -> $setup"
        return
    }
}

# Belt and braces: drop any "downloaded from the internet" mark the file might carry.
Unblock-File -Path $setup -ErrorAction SilentlyContinue

Write-Host '    Kurulum sihirbazi aciliyor (yonetici izni istenecek) / starting setup (asks for admin rights)...'
$start = @{ FilePath = $setup; Wait = $true; PassThru = $true }
if ($env:OBLIVION_SILENT) { $start.ArgumentList = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') }
$process = Start-Process @start

if ($downloaded) { Remove-Item $setup -Force -ErrorAction SilentlyContinue }
if ($process.ExitCode -ne 0) {
    throw "Kurulum tamamlanmadi / setup ended with code $($process.ExitCode)"
}
Write-Host '==> Tamam! Oblivion kuruldu. / Done!' -ForegroundColor Green
