param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

function Write-Wav {
    param(
        [string]$Path,
        [double]$Frequency,
        [double]$DurationMs,
        [double]$Volume = 0.35,
        [string]$Wave = 'sine'
    )

    $sampleRate = 44100
    $samples = [Math]::Max(1, [int]($sampleRate * $DurationMs / 1000.0))
    $data = New-Object byte[] ($samples * 2)
    for ($i = 0; $i -lt $samples; $i++) {
        $t = $i / $sampleRate
        $envelope = [Math]::Exp(-8.0 * $i / $samples)
        $raw = switch ($Wave) {
            'square' { if ([Math]::Sin(2.0 * [Math]::PI * $Frequency * $t) -ge 0) { 1.0 } else { -1.0 } }
            'triangle' { (2.0 / [Math]::PI) * [Math]::Asin([Math]::Sin(2.0 * [Math]::PI * $Frequency * $t)) }
            default { [Math]::Sin(2.0 * [Math]::PI * $Frequency * $t) }
        }
        $scaled = $raw * $envelope * $Volume * [int16]::MaxValue
        $clamped = [Math]::Min([int16]::MaxValue, [Math]::Max([int16]::MinValue, $scaled))
        $value = [int16]$clamped
        $bytes = [BitConverter]::GetBytes($value)
        $data[$i * 2] = $bytes[0]
        $data[$i * 2 + 1] = $bytes[1]
    }

    $dir = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($Path))
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
        $writer.Write([int](36 + $data.Length))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVE'))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('fmt '))
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]1)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]($sampleRate * 2))
        $writer.Write([int16]2)
        $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
        $writer.Write([int]$data.Length)
        $writer.Write($data)
    }
    finally {
        $writer.Dispose()
    }
}

$sounds = Join-Path $Root 'assets/sounds'
Write-Wav (Join-Path $sounds 'type.wav') 920 55 0.28 'triangle'
Write-Wav (Join-Path $sounds 'ding.wav') 1320 260 0.32 'sine'

$packs = @(
    @{ Name = 'ClassicTypewriter'; Wave = 'square'; Base = 760; Volume = 0.23 },
    @{ Name = 'SoftLaptop'; Wave = 'triangle'; Base = 1180; Volume = 0.16 },
    @{ Name = 'CyberTerminal'; Wave = 'sine'; Base = 1660; Volume = 0.20 }
)

foreach ($pack in $packs) {
    $packRoot = Join-Path $Root "assets/packs/$($pack.Name)"
    Write-Wav (Join-Path $packRoot 'normal/key01.wav') $pack.Base 42 $pack.Volume $pack.Wave
    Write-Wav (Join-Path $packRoot 'normal/key02.wav') ($pack.Base + 90) 46 $pack.Volume $pack.Wave
    Write-Wav (Join-Path $packRoot 'normal/key03.wav') ($pack.Base - 80) 50 $pack.Volume $pack.Wave
    Write-Wav (Join-Path $packRoot 'enter/ding.wav') ($pack.Base + 420) 240 ($pack.Volume + 0.08) 'sine'
    Write-Wav (Join-Path $packRoot 'space/space.wav') ($pack.Base - 180) 60 ($pack.Volume * 0.85) 'triangle'
    Write-Wav (Join-Path $packRoot 'backspace/backspace.wav') ($pack.Base + 220) 50 ($pack.Volume * 0.9) $pack.Wave
}

Write-Host "Generated placeholder SoundType WAV assets."
