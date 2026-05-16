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
$checksumPath = Join-Path $artifactsDir ("SoundType-{0}-{1}-portable.sha256" -f $Runtime, $Configuration)
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
    --self-contained true `
    --output $publishDir `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exePath = Join-Path $publishDir "SoundType.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish completed, but SoundType.exe was not found at $exePath."
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

$releaseDocs = @(
    @{ Source = Join-Path $repoRoot "README.md"; Destination = Join-Path $publishDir "README.md" },
    @{ Source = Join-Path $repoRoot "THIRD_PARTY_NOTICES.md"; Destination = Join-Path $publishDir "THIRD_PARTY_NOTICES.md" },
    @{ Source = Join-Path $repoRoot "docs\PRIVACY.md"; Destination = Join-Path $publishDir "PRIVACY.md" },
    @{ Source = Join-Path $repoRoot "docs\PACKAGING.md"; Destination = Join-Path $publishDir "PACKAGING.md" }
)

foreach ($doc in $releaseDocs) {
    Copy-Item -LiteralPath $doc.Source -Destination $doc.Destination -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$zipHash = Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath
"$($zipHash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $zipPath)" | Set-Content -Path $checksumPath -Encoding ascii

Write-Host "Published SoundType portable output:"
Write-Host "  Folder: $publishDir"
Write-Host "  Zip:    $zipPath"
Write-Host "  SHA256: $checksumPath"
