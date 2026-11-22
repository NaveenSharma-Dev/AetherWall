param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsDirectory = Join-Path $repositoryRoot 'artifacts'
$appOutputDirectory = Join-Path $artifactsDirectory 'app'
$setupOutputDirectory = Join-Path $artifactsDirectory 'setup'
$appProject = Join-Path $repositoryRoot 'AetherWall\AetherWall.csproj'
$setupProject = Join-Path $PSScriptRoot 'AetherWallSetup.csproj'

New-Item -ItemType Directory -Path $appOutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $setupOutputDirectory -Force | Out-Null

$appPublishArgs = @(
    'publish', $appProject,
    '--configuration', $Configuration,
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:IncludeAllContentForSelfExtract=true',
    '-o', $appOutputDirectory
)
& dotnet @appPublishArgs
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the AetherWall application failed with exit code $LASTEXITCODE."
}

$appPayload = Join-Path $appOutputDirectory 'AetherWall.exe'
if (-not (Test-Path -LiteralPath $appPayload -PathType Leaf)) {
    throw "The published application was not found at $appPayload."
}

$setupPublishArgs = @(
    'publish', $setupProject,
    '--configuration', $Configuration,
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:IncludeAllContentForSelfExtract=true',
    "-p:WallpaperExePath=$appPayload",
    '-o', $setupOutputDirectory
)
& dotnet @setupPublishArgs
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the AetherWall installer failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $setupOutputDirectory 'AetherWall-Setup.exe'
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "The installer was not found at $installerPath."
}

Write-Host "Installer created: $installerPath"
