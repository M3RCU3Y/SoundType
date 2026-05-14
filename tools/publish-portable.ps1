param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$projectPath = Join-Path $repoRoot "src\SoundType.App\SoundType.App.csproj"
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "publish\SoundType"
$zipPath = Join-Path $artifactsDir ("SoundType-{0}-{1}-portable.zip" -f $Runtime, $Configuration)
$localDotnet = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"

if (Test-Path -LiteralPath $localDotnet) {
    $dotnet = $localDotnet
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCommand) {
        throw "Could not find .tools\dotnet\dotnet.exe or dotnet on PATH."
    }

    $dotnet = $dotnetCommand.Source
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null

& $dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    --output $publishDir `
    -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $publishDir "SoundType.App.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish completed, but SoundType.App.exe was not found at $exePath."
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Published SoundType portable output:"
Write-Host "  Folder: $publishDir"
Write-Host "  Zip:    $zipPath"
