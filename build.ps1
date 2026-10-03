#requires -Version 5.1
<#
    Oblivion - one-click build script (Windows).

    EASIEST: just double-click  build.bat  (keeps the window open and shows errors).

    Or run this directly:
        powershell -ExecutionPolicy Bypass -File .\build.ps1

    It will:
      1. Make sure the .NET 8 SDK is available (installs a local copy if not).
      2. Publish Oblivion (self-contained, ReadyToRun)        ->  dist\app\
      3. Produce a portable single-file executable            ->  dist\Oblivion.exe
      4. Build the installer (Setup.exe)                      ->  dist\OblivionSetup.exe   (if Inno Setup is present)
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }

function PauseExit($code) {
    Write-Host ""
    try { Read-Host "Press Enter to close this window" | Out-Null } catch { }
    exit $code
}

try {
    $ErrorActionPreference = "Stop"
    $root = Split-Path -Parent $MyInvocation.MyCommand.Definition
    Set-Location $root
    Write-Host "Oblivion build - working folder: $root" -ForegroundColor Green

    # -----------------------------------------------------------------------
    # 1. Ensure the .NET 8 SDK
    # -----------------------------------------------------------------------
    function Get-DotnetCommand {
        $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if ($cmd) {
            $sdks = & dotnet --list-sdks 2>$null
            if ($sdks -match '^8\.') {
                Write-Host "Found .NET 8 SDK." -ForegroundColor Green
                return "dotnet"
            }
            Write-Host "dotnet exists but no .NET 8 SDK - installing a local copy." -ForegroundColor Yellow
        }
        else {
            Write-Host "dotnet not found - installing a local .NET 8 SDK (no admin needed)." -ForegroundColor Yellow
        }

        Write-Step "Installing local .NET 8 SDK"
        $installDir = Join-Path $root ".dotnet"
        $installScript = Join-Path $env:TEMP "dotnet-install.ps1"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing
        & $installScript -Channel 8.0 -InstallDir $installDir -NoPath
        $local = Join-Path $installDir "dotnet.exe"
        if (-not (Test-Path $local)) { throw "Failed to install the .NET 8 SDK." }
        return $local
    }

    $dotnet = Get-DotnetCommand
    Write-Host "Using dotnet: $dotnet"
    & $dotnet --version

    $dist = Join-Path $root "dist"
    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    New-Item -ItemType Directory -Path $dist | Out-Null

    # -----------------------------------------------------------------------
    # 2. Self-contained ReadyToRun folder (used by the installer: fastest start-up)
    # -----------------------------------------------------------------------
    Write-Step "Publishing Oblivion (installer build) - this can take a few minutes"
    & $dotnet publish "src\Vanish\Vanish.csproj" `
        -c $Configuration `
        -r $Runtime `
        --self-contained `
        -p:PublishReadyToRun=true `
        -p:DebugType=None `
        -o "$dist\app"
    if ($LASTEXITCODE -ne 0) {
        throw "BUILD FAILED. Please copy ALL the red/error text above and send it to me."
    }

    # -----------------------------------------------------------------------
    # 3. Portable single-file executable
    # -----------------------------------------------------------------------
    Write-Step "Publishing Oblivion (portable single file)"
    & $dotnet publish "src\Vanish\Vanish.csproj" `
        -c $Configuration `
        -r $Runtime `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:DebugType=None `
        -o "$dist\portable"
    if ($LASTEXITCODE -ne 0) {
        throw "BUILD FAILED. Please copy ALL the red/error text above and send it to me."
    }

    Copy-Item "$dist\portable\Oblivion.exe" "$dist\Oblivion.exe" -Force
    Write-Host "`nPortable build ready:" -ForegroundColor Green
    Write-Host "    $dist\Oblivion.exe" -ForegroundColor Green

    # -----------------------------------------------------------------------
    # 4. Installer via Inno Setup (iscc.exe)
    # -----------------------------------------------------------------------
    Write-Step "Building installer (optional)"
    $iscc = $null
    $c = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($c) { $iscc = $c.Source }
    if (-not $iscc) {
        $candidates = @(
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
            (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
        )
        foreach ($p in $candidates) {
            if (Test-Path $p) { $iscc = $p; break }
        }
    }

    if ($iscc) {
        & $iscc "installer\Vanish.iss"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`nInstaller ready:" -ForegroundColor Green
            Write-Host "    $dist\OblivionSetup.exe" -ForegroundColor Green
        }
        else {
            Write-Host "Installer step failed, but the portable Oblivion.exe is ready." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Inno Setup not found - skipping installer (the portable Oblivion.exe runs without it)." -ForegroundColor Yellow
        Write-Host "For OblivionSetup.exe, install Inno Setup 6 from https://jrsoftware.org/isdl.php and re-run."
    }

    Write-Step "DONE"
    Write-Host "Double-click  dist\Oblivion.exe  to run Oblivion (it will ask for administrator rights)." -ForegroundColor Green
    PauseExit 0
}
catch {
    Write-Host ""
    Write-Host "########################################################" -ForegroundColor Red
    Write-Host "# Something went wrong:" -ForegroundColor Red
    Write-Host ("# " + $_.Exception.Message) -ForegroundColor Red
    Write-Host "########################################################" -ForegroundColor Red
    Write-Host "Send me the text above and I will fix it." -ForegroundColor Yellow
    PauseExit 1
}
