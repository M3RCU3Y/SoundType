# SoundType

**A Windows typing sound studio for mechanical switches and typewriters.**

SoundType runs quietly in the tray, listens locally for key presses, and plays customizable sound packs without recording words, characters, or key history.

```text
Local keyboard events -> privacy-safe key groups -> pack rules -> mixer/EQ -> low-latency playback
```

## Highlights

| Area | What SoundType Does |
| --- | --- |
| Sound packs | Built-in sourced switch and typewriter packs |
| Authentic profiles | MIT/CC0 profiles including Cherry MX, Gateron Ink, Holy Panda, Alpaca, NovelKeys Cream, Logitech G915 Brown, Mechvibes full-travel switches, Opera GX, Cherry KC 1000, and curated typewriter recordings |
| Realistic switch motion | KSP-style press and release samples for supported mechanical profiles |
| Keyboard control | Full visual keyboard for excluding keys without typing raw characters |
| Per-app rules | Disable, force enable, override pack, or adjust volume by foreground app |
| Mixer | Master volume, group volumes, pitch variation, limiter, and a simple Spotify-like EQ |
| Tray behavior | Open, hide, mute, see active pack, and exit from the Windows tray |
| Startup | Optional Windows sign-in autostart from the settings panel |
| Pack workflow | Import/export `.soundpack`, validate packs, and convert compatible Mechvibes folders |
| Privacy | No typed text storage, no key history, no network keystroke sending |

## Built-In Packs

| Pack | Feel |
| --- | --- |
| KSP switch profiles | MIT-licensed Alpaca, Cherry MX, Gateron Ink, Holy Panda, NovelKeys Cream, Logitech G915 Brown, and Opera GX profiles with press/release samples where available |
| Mechvibes full-travel profiles | MIT-licensed Turquoise, Cream, MX Black, MX Brown, and MX Blue full-travel profiles with release samples where available |
| Cherry KC 1000 Real Keys | CC0 single-key recordings from OpenGameArt |
| Curated typewriter profiles | CC0 Freesound, Chosic, and OpenGameArt typewriter profiles segmented into one-shot samples |
| Red Library Manual Typewriter | CC0 high-variation manual typewriter keys with dedicated carriage-return and margin-bell Enter samples |

The bundled audio now favors sourced profiles over generated placeholders. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Quick Start

Developer requirements:

- Windows 10 or Windows 11
- .NET 8 SDK, or the local ignored SDK at `.tools/dotnet` used in this workspace

Run the app:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\src\SoundType.App\SoundType.App.csproj
```

Build and test:

```powershell
.\.tools\dotnet\dotnet.exe build .\SoundType.sln
.\.tools\dotnet\dotnet.exe test .\SoundType.sln
```

Package a portable zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-portable.ps1
```

The portable build is written to `artifacts\SoundType-win-x64-Release-portable.zip`.
It is self-contained for Windows x64, so end users do not need to install the .NET runtime separately. A matching SHA-256 checksum is written beside the zip.

## Using SoundType

1. Pick a pack from the Sound Library.
2. Adjust master volume, pitch variation, and group volumes.
3. Use the Equalizer presets for quick tone shaping.
4. Click keys on the visual keyboard to exclude noisy or unwanted keys.
5. Add app rules for games, calls, editors, or streaming tools.
6. Enable "Keep SoundType running in the tray when closed" for background use.
7. Enable "Start SoundType when I sign in to Windows" if you want it always ready.

The global mute hotkey defaults to `Ctrl+Alt+K`.

## Sound Packs

SoundType folder packs use a `pack.json` manifest plus grouped WAV/MP3 samples. Archives use the `.soundpack` extension and are validated before import.

Validate a folder pack or archive:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\assets\packs\Freesound-RoyalQuietDeluxe
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\dist\MyPack.soundpack
```

Convert a compatible Mechvibes folder pack:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.MechvibesImporter\SoundType.MechvibesImporter.csproj -- C:\path\to\mechvibes-pack .\dist\ConvertedPack
```

See [docs/SOUND_PACK_FORMAT.md](docs/SOUND_PACK_FORMAT.md) for the pack contract.

## Repository Map

| Path | Purpose |
| --- | --- |
| `src/SoundType.App` | WPF app, tray UI, settings UI, visual keyboard, app rules |
| `src/SoundType.Audio` | Audio engine, decoding, EQ, limiter, pitch variation |
| `src/SoundType.Core` | Settings, pack models, validation, rules |
| `src/SoundType.Input` | Keyboard hook, hotkey, active window detection |
| `src/SoundType.Tests` | Unit and integration-style regression tests |
| `assets/packs` | Built-in sourced sound packs |
| `tools` | Pack generator, validator, importer, portable publish script |
| `docs` | Build spec, privacy notes, roadmap, QA, packaging, development docs |

## Development Docs

- [Development guide](docs/DEVELOPMENT.md)
- [Manual QA checklist](docs/QA_CHECKLIST.md)
- [Packaging guide](docs/PACKAGING.md)
- [Privacy notes](docs/PRIVACY.md)
- [Roadmap](docs/ROADMAP.md)

## Privacy

SoundType reacts to key-down events so it can play sounds. It stores preferences such as volume, excluded keys, selected pack, and app rules under `%AppData%\SoundType\settings.json`.

It does not store typed words, typed characters, passwords, or a key history, and it does not send keystrokes to a server. See [docs/PRIVACY.md](docs/PRIVACY.md).

## Status

SoundType is an active desktop build. The core app, tray behavior, startup setting, pack validation, built-in audio, importer, EQ, tests, and self-contained portable packaging are implemented. The next polish targets are code signing, richer pack marketplace-style browsing, and more manual UI QA on physical Windows machines.
