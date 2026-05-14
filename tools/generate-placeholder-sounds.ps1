param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'generate-original-sound-packs.ps1') -Root $Root
