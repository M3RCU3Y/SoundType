# Development Guide

This repo is organized so the app can keep improving without mixing UI, input, audio, and pack tooling together.

## Setup

Use a normal .NET 8 SDK if it is installed:

```powershell
dotnet build .\SoundType.sln
dotnet test .\SoundType.sln
```

In this workspace, a local SDK is available at `.tools/dotnet`:

```powershell
.\.tools\dotnet\dotnet.exe build .\SoundType.sln
.\.tools\dotnet\dotnet.exe test .\SoundType.sln
```

The `.tools` folder is intentionally ignored and should stay out of commits.

## Run Locally

```powershell
.\.tools\dotnet\dotnet.exe run --project .\src\SoundType.App\SoundType.App.csproj
```

SoundType stores user settings at `%AppData%\SoundType\settings.json`.

## Project Boundaries

| Project | Owns |
| --- | --- |
| `SoundType.App` | WPF screens, tray menu, user interaction, import/export dialogs |
| `SoundType.Audio` | Playback queue, NAudio integration, decoding, pitch, EQ, limiter |
| `SoundType.Core` | Pack contracts, settings contracts, app-rule decisions, validation |
| `SoundType.Input` | Keyboard hook, global hotkey, active foreground app detection |
| `SoundType.Tests` | Regressions around packs, rules, settings, audio primitives, import tooling |

Prefer adding logic to the lowest project that owns the behavior. UI code should orchestrate services, not duplicate validation or audio rules.

## Branch Policy

The repository is currently cleaned back to `main` only. If future feature branches are needed, delete merged branches after the PR lands so Codex and humans both start from an obvious base.

## Verification

Before pushing app changes, run:

```powershell
.\.tools\dotnet\dotnet.exe build .\SoundType.sln
.\.tools\dotnet\dotnet.exe test .\SoundType.sln
```

For changes touching tray, startup, or audio, also run the manual checklist in [QA_CHECKLIST.md](QA_CHECKLIST.md).

## Ignored Local Folders

| Folder | Why ignored |
| --- | --- |
| `.tools` | Local SDK/runtime helpers |
| `.external` | Temporary reference repos or audio research |
| `.worktrees` | Temporary git worktrees for parallel implementation |
| `artifacts` | Build and packaging outputs |

Keep committed source, docs, assets, tests, and scripts in the visible repo tree.
