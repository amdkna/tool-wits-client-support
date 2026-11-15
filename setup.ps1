param(
    [switch]$NoRun,
    [switch]$NoUpdate
)

$ErrorActionPreference = 'Stop'
$repoUrl = 'https://github.com/amdkna/tool-wits-client-support.git'
$root = $PSScriptRoot

function Step([string]$message) { Write-Host "[WITS] $message" -ForegroundColor Cyan }
function Fail([string]$message) { throw "[WITS] $message" }

function Refresh-Path {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user;$env:Path"
}

function Ensure-Git {
    if (Get-Command git -ErrorAction SilentlyContinue) { return }
    Step 'Git not found. Attempting installation with winget...'
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Fail 'Git is required. Install it from https://git-scm.com/download/win and rerun setup.bat.'
    }
    & winget install --id Git.Git --exact --source winget --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { Fail 'Automatic Git installation failed.' }
    Refresh-Path
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        $candidate = Join-Path $env:ProgramFiles 'Git\cmd'
        if (Test-Path (Join-Path $candidate 'git.exe')) { $env:Path = "$candidate;$env:Path" }
    }
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Fail 'Git was installed but not detected. Open a new terminal and run setup.bat again.'
    }
}

try {
    Step 'Preparing development environment'
    Ensure-Git

    if (-not (Test-Path (Join-Path $root '.git'))) {
        $destination = Join-Path $HOME 'source\tool-wits-client-support'
        Step "Cloning repository to $destination"
        if (Test-Path $destination) {
            Fail "Destination already exists: $destination. Run setup.bat from that repository."
        }
        New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
        & git clone $repoUrl $destination
        if ($LASTEXITCODE -ne 0) { Fail 'Git clone failed.' }
        $forward = @()
        if ($NoRun) { $forward += '-NoRun' }
        if ($NoUpdate) { $forward += '-NoUpdate' }
        & (Join-Path $destination 'setup.ps1') @forward
        exit $(if ($?) { 0 } else { 1 })
    }

    Set-Location $root
    if (-not $NoUpdate) {
        Step 'Checking latest version on GitHub (main)...'
        & git fetch origin main
        if ($LASTEXITCODE -ne 0) { Fail 'Could not fetch the latest GitHub changes.' }
        $status = & git status --porcelain
        if ($LASTEXITCODE -ne 0) { Fail 'Could not check working tree status.' }
        if ($status) {
            Step 'Local changes detected; skipping pull to avoid overwriting your work.'
        }
        else {
            & git pull --ff-only origin main
            if ($LASTEXITCODE -ne 0) { Fail 'Could not fast-forward to main. Resolve Git divergence first.' }
        }
        $localSha = (& git rev-parse --short HEAD).Trim()
        $remoteSha = (& git rev-parse --short origin/main).Trim()
        Step "Local revision: $localSha | Remote main: $remoteSha"
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    $sdkInstalled = $false
    if ($dotnet) {
        $sdkInstalled = @(& dotnet --list-sdks) | Where-Object { $_ -match '^10\.' } | Select-Object -First 1
    }
    if (-not $sdkInstalled) {
        Step 'Installing .NET 10 SDK locally for this user (no administrator access required)...'
        $installDir = Join-Path $HOME '.dotnet'
        $installScript = Join-Path $env:TEMP 'wits-dotnet-install.ps1'
        Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript -UseBasicParsing
        & $installScript -Channel '10.0' -InstallDir $installDir
        if (-not $?) { Fail '.NET 10 SDK installation failed.' }
        $env:DOTNET_ROOT = $installDir
        $env:Path = "$installDir;$env:Path"
    }

    $sdkCheck = @(& dotnet --list-sdks) | Where-Object { $_ -match '^10\.' }
    if (-not $sdkCheck) { Fail '.NET 10 SDK is still unavailable.' }
    Step ("Found .NET SDK: " + ($sdkCheck | Select-Object -Last 1))

    $project = Join-Path $root 'src\Wits.ClientSupport\Wits.ClientSupport.csproj'
    $output = Join-Path $root 'publish\win-x64'
    Step 'Publishing Windows x64 self-contained, single-file executable...'
    & dotnet publish $project -c Release -r win-x64 --self-contained true -o $output -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) { Fail 'Build/publish failed.' }

    $exe = Join-Path $output 'WITS.ClientSupport.exe'
    if (-not (Test-Path $exe)) { Fail 'Build completed but executable was not found.' }
    Step "Build successful: $exe"

    if (-not $NoRun) {
        Step 'Launching WITS Client Support...'
        Start-Process -FilePath $exe
    }
    exit 0
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
