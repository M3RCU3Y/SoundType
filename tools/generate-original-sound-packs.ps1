param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$SampleRate = 44100

function New-EmptyBuffer {
    param([double]$DurationMs)

    $samples = [Math]::Max(1, [int]($SampleRate * $DurationMs / 1000.0))
    return New-Object double[] $samples
}

function Add-SineBurst {
    param(
        [double[]]$Buffer,
        [double]$Frequency,
        [double]$Amplitude,
        [double]$Decay,
        [double]$StartMs = 0,
        [double]$DurationMs = 0
    )

    $start = [int]($SampleRate * $StartMs / 1000.0)
    $end = if ($DurationMs -gt 0) {
        [Math]::Min($Buffer.Length, $start + [int]($SampleRate * $DurationMs / 1000.0))
    } else {
        $Buffer.Length
    }

    for ($i = $start; $i -lt $end; $i++) {
        $local = ($i - $start) / $SampleRate
        $env = [Math]::Exp(-$Decay * $local)
        $Buffer[$i] += [Math]::Sin(2.0 * [Math]::PI * $Frequency * $local) * $Amplitude * $env
    }
}

function Add-NoiseBurst {
    param(
        [double[]]$Buffer,
        [Random]$Random,
        [double]$Amplitude,
        [double]$StartMs,
        [double]$DurationMs,
        [double]$Decay = 80.0
    )

    $start = [int]($SampleRate * $StartMs / 1000.0)
    $end = [Math]::Min($Buffer.Length, $start + [int]($SampleRate * $DurationMs / 1000.0))
    for ($i = $start; $i -lt $end; $i++) {
        $local = ($i - $start) / $SampleRate
        $env = [Math]::Exp(-$Decay * $local)
        $Buffer[$i] += (($Random.NextDouble() * 2.0) - 1.0) * $Amplitude * $env
    }
}

function Add-ClickPair {
    param(
        [double[]]$Buffer,
        [Random]$Random,
        [double]$Brightness,
        [double]$Weight,
        [double]$SeparationMs
    )

    Add-NoiseBurst $Buffer $Random ($Brightness * 0.55) 0.0 4.5 190.0
    Add-SineBurst $Buffer (2600 + $Brightness * 1400) ($Brightness * 0.22) 260.0 0.0 18.0
    Add-SineBurst $Buffer (170 + $Weight * 210) ($Weight * 0.26) 46.0 1.2 70.0
    Add-NoiseBurst $Buffer $Random ($Brightness * 0.28) $SeparationMs 3.0 210.0
}

function Normalize-Buffer {
    param([double[]]$Buffer)

    $max = 0.0
    foreach ($sample in $Buffer) {
        $abs = [Math]::Abs($sample)
        if ($abs -gt $max) {
            $max = $abs
        }
    }

    if ($max -le 0.001) {
        return
    }

    $gain = [Math]::Min(0.92 / $max, 1.0)
    for ($i = 0; $i -lt $Buffer.Length; $i++) {
        $Buffer[$i] *= $gain
    }
}

function Write-WavBuffer {
    param(
        [string]$Path,
        [double[]]$Buffer
    )

    Normalize-Buffer $Buffer
    $data = New-Object byte[] ($Buffer.Length * 2)
    for ($i = 0; $i -lt $Buffer.Length; $i++) {
        $scaled = $Buffer[$i] * 0.82 * [int16]::MaxValue
        $clamped = [Math]::Min([int16]::MaxValue, [Math]::Max([int16]::MinValue, $scaled))
        $bytes = [BitConverter]::GetBytes([int16]$clamped)
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
        $writer.Write([int]$SampleRate)
        $writer.Write([int]($SampleRate * 2))
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

function New-KeySound {
    param(
        [string]$Kind,
        [double]$Brightness,
        [double]$Weight,
        [double]$DurationMs,
        [int]$Seed,
        [double]$Variation = 0.0
    )

    $random = [Random]::new($Seed)
    $buffer = New-EmptyBuffer $DurationMs
    switch ($Kind) {
        'linear' {
            Add-NoiseBurst $buffer $random (0.12 + $Brightness * 0.13 + $Variation) 0.0 5.0 150.0
            Add-SineBurst $buffer (150 + $Weight * 160 + $Variation * 80) (0.20 + $Weight * 0.20) 42.0 0.4 78.0
            Add-SineBurst $buffer (820 + $Brightness * 420) (0.045 + $Brightness * 0.04) 92.0 1.0 42.0
        }
        'tactile' {
            Add-NoiseBurst $buffer $random (0.18 + $Brightness * 0.16) 0.0 4.2 170.0
            Add-NoiseBurst $buffer $random (0.08 + $Brightness * 0.08) 8.0 3.0 150.0
            Add-SineBurst $buffer (190 + $Weight * 180) (0.26 + $Weight * 0.16) 50.0 0.8 82.0
            Add-SineBurst $buffer (1200 + $Brightness * 520) (0.06 + $Brightness * 0.05) 120.0 2.0 36.0
        }
        'clicky' {
            Add-ClickPair $buffer $random (0.35 + $Brightness) (0.30 + $Weight) (5.2 + $Variation * 2.0)
            Add-SineBurst $buffer (3100 + $Brightness * 1100) (0.07 + $Brightness * 0.05) 180.0 1.0 35.0
        }
        'thock' {
            Add-NoiseBurst $buffer $random (0.10 + $Brightness * 0.08) 0.0 5.0 110.0
            Add-SineBurst $buffer (92 + $Weight * 74) (0.42 + $Weight * 0.18) 31.0 0.8 120.0
            Add-SineBurst $buffer (310 + $Weight * 140) (0.16 + $Brightness * 0.05) 58.0 1.0 82.0
        }
        'typewriter' {
            Add-NoiseBurst $buffer $random (0.32 + $Brightness * 0.18) 0.0 4.0 260.0
            Add-SineBurst $buffer (1750 + $Brightness * 720) (0.17 + $Brightness * 0.07) 160.0 0.0 28.0
            Add-SineBurst $buffer (430 + $Weight * 150) (0.23 + $Weight * 0.12) 42.0 1.5 92.0
            Add-NoiseBurst $buffer $random (0.12 + $Brightness * 0.07) 18.0 5.0 120.0
        }
        default {
            Add-NoiseBurst $buffer $random 0.18 0.0 5.0 150.0
            Add-SineBurst $buffer 320 0.24 40.0 0.5 80.0
        }
    }

    return $buffer
}

function Write-Pack {
    param(
        [hashtable]$Pack
    )

    $packRoot = Join-Path $Root "assets/packs/$($Pack.Folder)"
    if (Test-Path $packRoot) {
        Remove-Item -LiteralPath $packRoot -Recurse -Force
    }

    $normal = @()
    for ($i = 1; $i -le 6; $i++) {
        $name = "normal/key{0:D2}.wav" -f $i
        $normal += $name
        $buffer = New-KeySound $Pack.Kind $Pack.Brightness $Pack.Weight $Pack.Duration ($Pack.Seed + $i) (($i - 3) * 0.015)
        Write-WavBuffer (Join-Path $packRoot $name) $buffer
    }

    $enter = New-KeySound $Pack.Kind ($Pack.Brightness + 0.08) ($Pack.Weight + 0.15) ($Pack.Duration + 36) ($Pack.Seed + 101) 0.03
    Add-SineBurst $enter (760 + $Pack.Weight * 360) 0.18 18.0 22.0 170.0
    Write-WavBuffer (Join-Path $packRoot 'enter/enter.wav') $enter

    $space = New-KeySound $Pack.Kind ($Pack.Brightness - 0.10) ($Pack.Weight + 0.22) ($Pack.Duration + 18) ($Pack.Seed + 202) -0.02
    Write-WavBuffer (Join-Path $packRoot 'space/space.wav') $space

    $backspace = New-KeySound $Pack.Kind ($Pack.Brightness + 0.06) ($Pack.Weight - 0.06) ($Pack.Duration - 6) ($Pack.Seed + 303) 0.01
    Write-WavBuffer (Join-Path $packRoot 'backspace/backspace.wav') $backspace

    $tab = New-KeySound $Pack.Kind ($Pack.Brightness - 0.02) ($Pack.Weight + 0.05) ($Pack.Duration + 4) ($Pack.Seed + 404) 0.0
    Write-WavBuffer (Join-Path $packRoot 'tab/tab.wav') $tab

    $manifest = [ordered]@{
        id = $Pack.Id
        name = $Pack.Name
        author = 'SoundType'
        version = '1.0.0'
        description = $Pack.Description
        license = 'Original synthetic audio generated for SoundType; MIT-compatible for this project.'
        tags = $Pack.Tags
        groups = [ordered]@{
            normal = $normal
            enter = @('enter/enter.wav')
            space = @('space/space.wav')
            backspace = @('backspace/backspace.wav')
            tab = @('tab/tab.wav')
        }
        keyOverrides = [ordered]@{
            Enter = 'enter'
            Space = 'space'
            Backspace = 'backspace'
            Tab = 'tab'
        }
        defaults = [ordered]@{
            volume = $Pack.Volume
            pitchVariation = 0.0
            randomize = $true
        }
    }

    $json = $manifest | ConvertTo-Json -Depth 8
    Set-Content -LiteralPath (Join-Path $packRoot 'pack.json') -Value $json -Encoding UTF8
}

$packs = @(
    @{ Folder = 'ClickyBlueSwitches'; Id = 'clicky-blue-switches'; Name = 'Clicky Blue Switches'; Kind = 'clicky'; Brightness = 0.62; Weight = 0.26; Duration = 82; Volume = 0.62; Seed = 1200; Tags = @('switch', 'clicky', 'bright'); Description = 'Bright click jacket snap with a crisp reset tick.' },
    @{ Folder = 'TactileBrownSwitches'; Id = 'tactile-brown-switches'; Name = 'Tactile Brown Switches'; Kind = 'tactile'; Brightness = 0.34; Weight = 0.34; Duration = 76; Volume = 0.58; Seed = 2200; Tags = @('switch', 'tactile', 'balanced'); Description = 'Rounded tactile bump with a restrained plastic clack.' },
    @{ Folder = 'LinearRedSwitches'; Id = 'linear-red-switches'; Name = 'Linear Red Switches'; Kind = 'linear'; Brightness = 0.22; Weight = 0.28; Duration = 66; Volume = 0.52; Seed = 3200; Tags = @('switch', 'linear', 'smooth'); Description = 'Clean linear taps with low spring noise for fast typing.' },
    @{ Folder = 'DeepThockSwitches'; Id = 'deep-thock-switches'; Name = 'Deep Thock Switches'; Kind = 'thock'; Brightness = 0.16; Weight = 0.76; Duration = 106; Volume = 0.64; Seed = 4200; Tags = @('switch', 'thock', 'deep'); Description = 'Low damped thock with a soft case resonance.' },
    @{ Folder = 'ClassicTypewriter'; Id = 'classic-typewriter'; Name = 'Classic Typewriter'; Kind = 'typewriter'; Brightness = 0.48; Weight = 0.58; Duration = 98; Volume = 0.70; Seed = 5200; Tags = @('typewriter', 'vintage', 'metal'); Description = 'Vintage mechanical slugs, ribbon chatter, and a stronger carriage enter.' },
    @{ Folder = 'NewsroomTypewriter'; Id = 'newsroom-typewriter'; Name = 'Newsroom Typewriter'; Kind = 'typewriter'; Brightness = 0.68; Weight = 0.46; Duration = 86; Volume = 0.66; Seed = 6200; Tags = @('typewriter', 'bright', 'fast'); Description = 'Fast bright newsroom keys with sharper metal return energy.' },
    @{ Folder = 'SoftLaptop'; Id = 'soft-laptop'; Name = 'Soft Laptop'; Kind = 'linear'; Brightness = 0.12; Weight = 0.18; Duration = 54; Volume = 0.42; Seed = 7200; Tags = @('laptop', 'quiet', 'soft'); Description = 'Quiet scissor-switch taps for long writing sessions.' },
    @{ Folder = 'CyberTerminal'; Id = 'cyber-terminal'; Name = 'Cyber Terminal'; Kind = 'clicky'; Brightness = 0.42; Weight = 0.12; Duration = 58; Volume = 0.48; Seed = 8200; Tags = @('digital', 'terminal', 'glassy'); Description = 'Compact glassy ticks with a clean terminal response.' }
)

$sounds = Join-Path $Root 'assets/sounds'
Write-WavBuffer (Join-Path $sounds 'type.wav') (New-KeySound 'tactile' 0.28 0.22 62 9101 0.0)
$ding = New-KeySound 'typewriter' 0.36 0.38 180 9102 0.0
Add-SineBurst $ding 1180 0.24 12.0 14.0 210.0
Write-WavBuffer (Join-Path $sounds 'ding.wav') $ding

foreach ($pack in $packs) {
    Write-Pack $pack
}

Write-Host "Generated $($packs.Count) original SoundType packs."
