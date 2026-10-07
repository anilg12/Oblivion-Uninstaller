# Oblivion one-line installer for windows 10/11
#
#   irm https://raw.githubusercontent.com/anilg12/Oblivion-Uninstaller/main/install.ps1 | iex
#
# downloads the latest setup from github releases and runs it. files downloaded by powershell
# don't get the mark-of-the-web, so smartscreen doesn't block the setup or the app.
#
# env vars (used by ci):
#   OBLIVION_SETUP          local setup path, skips the download
#   OBLIVION_SILENT=1       silent install
#   OBLIVION_DOWNLOAD_ONLY  only download the setup to this path
#   OBLIVION_SKIP_API=1     don't use the github api (tests the fallback)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # iwr is way faster without the progress bar
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$repo = 'anilg12/Oblivion-Uninstaller'
Write-Host ''
Write-Host '==> Oblivion kuruluyor / installing...' -ForegroundColor Magenta

$setup = $env:OBLIVION_SETUP
$downloaded = $false
if (-not $setup) {
    $tag = $null; $name = $null; $url = $null
    try {
        if ($env:OBLIVION_SKIP_API) { throw 'skip the API' }
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" `
            -Headers @{ 'User-Agent' = 'Oblivion-Installer'; 'Accept' = 'application/vnd.github+json' }
        $asset = $release.assets | Where-Object { $_.name -like '*-Windows-Setup.exe' } | Select-Object -First 1
        if ($asset) { $tag = $release.tag_name; $name = $asset.name; $url = $asset.browser_download_url }
    } catch {
        # github api = 60 anonymous requests/hour per ip, fall back to the releases/latest redirect
        $request = [Net.WebRequest]::Create("https://github.com/$repo/releases/latest")
        $request.AllowAutoRedirect = $false
        $request.UserAgent = 'Oblivion-Installer'
        $response = $request.GetResponse()
        $location = $response.Headers['Location']
        $response.Close()
        if ($location -match '/tag/(v?([\d.]+))$') {
            $tag = $Matches[1]
            $name = "Oblivion-$($Matches[2])-Windows-Setup.exe"
            $url = "https://github.com/$repo/releases/download/$tag/$name"
        }
    }
    if (-not $url) {
        throw "Kurulum dosyasi bulunamadi / no Windows setup found: https://github.com/$repo/releases"
    }
    $setup = if ($env:OBLIVION_DOWNLOAD_ONLY) { $env:OBLIVION_DOWNLOAD_ONLY } else { Join-Path $env:TEMP $name }
    Write-Host "    ${tag}: $url"
    Invoke-WebRequest -Uri $url -OutFile $setup -UseBasicParsing
    $downloaded = $true
    if ($env:OBLIVION_DOWNLOAD_ONLY) {
        Write-Host "    -> $setup"
        return
    }
}

# just in case, remove the mark-of-the-web if it's there
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
