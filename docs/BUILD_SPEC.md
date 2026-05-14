# SoundType Build Spec

SoundType is a local-only Windows desktop utility that plays customizable typing sounds on global keypresses.

## First Implementation Scope

- Windows desktop app using C#/.NET.
- WPF frontend as a practical fallback while preserving service boundaries for a future WinUI 3 shell.
- Global keyboard hook.
- Privacy-safe key identities only.
- Local JSON settings.
- NAudio playback from preloaded WAV/MP3 sound packs.
- Built-in sound pack folder format.
- Enable/mute, master volume, key exclusions, app exclusions, tray behavior, startup registration, and basic EQ trim controls.

## Privacy Rules

SoundType must not record typed words, log typed characters, save key history, transmit keystrokes, require an account, or enable telemetry by default.

Persisted data may include user settings, sound pack metadata, excluded key names, and app process names used for rules.

## Architecture

```text
SoundType.App
  -> SoundType.Core
  -> SoundType.Input
  -> SoundType.Audio
```

- `SoundType.Core` owns settings models, sound pack models, and rule decisions.
- `SoundType.Input` owns keyboard hooks, active-window process detection, and key identity mapping.
- `SoundType.Audio` owns sound pack validation/loading and queued playback.
- `SoundType.App` owns the WPF UI, tray menu, startup setting, and service wiring.
