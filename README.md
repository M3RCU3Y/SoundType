# SoundType

SoundType is a Windows typing sound studio. It lets everyday typing use local, customizable sound packs such as typewriter clicks, soft laptop taps, and sci-fi terminal ticks.

SoundType runs locally and does not record what you type.

## Current Features

- Global low-level keyboard hook for key-down events.
- Privacy-safe key identities only; no typed text, words, or key history.
- Non-blocking audio queue backed by preloaded WAV sound packs.
- Normal, Enter, Space, and Backspace sound groups.
- Enable/mute toggle, master volume, key exclusions, app exclusions, tray menu, and start-with-Windows setting.
- Global `Ctrl+Alt+K` mute toggle hotkey.
- `.soundpack` archive import/export with safe zip extraction.
- Per-app rule modes for disabled/default/enabled-only behavior, pack override, and volume override.
- JSON settings stored under `%AppData%\SoundType\settings.json`.
- Three built-in placeholder packs: Classic Typewriter, Soft Laptop, and Cyber Terminal.
- WPF frontend using the same service boundaries planned for a future WinUI 3 frontend.

## Requirements

- Windows 10/11.
- .NET 8 SDK to build from source.

This repo includes no checked-in SDK. During this Codex build, a local SDK was installed into `.tools/dotnet`, which is ignored by Git.

## Run

```powershell
.\.tools\dotnet\dotnet.exe run --project .\src\SoundType.App\SoundType.App.csproj
```

Or, if `dotnet` is on your PATH:

```powershell
dotnet run --project .\src\SoundType.App\SoundType.App.csproj
```

## Test

```powershell
.\.tools\dotnet\dotnet.exe test .\SoundType.sln
```

## Package

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-portable.ps1
```

This creates `artifacts\SoundType-win-x64-Release-portable.zip`. See [docs/PACKAGING.md](docs/PACKAGING.md).

## Placeholder Audio

The built-in WAV files are generated development placeholders:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\generate-placeholder-sounds.ps1
```

Replace them with polished short samples before a public release.

## Privacy

SoundType listens for keyboard events only so it can play a sound when a key is pressed. It does not record typed words, typed characters, or key history. It does not send keystrokes anywhere.

See [docs/PRIVACY.md](docs/PRIVACY.md).
