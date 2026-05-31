# Repository Guide

This is the fast orientation page for future SoundType work.

## Current App Shape

SoundType is a Windows WPF tray app. It is not a web app and it does not use a local browser server.

```text
SoundType.App   -> WPF screens, tray menu, startup wiring, view models
SoundType.Core  -> settings models, pack metadata, app-rule decisions
SoundType.Input -> keyboard hook, active-window lookup, global hotkey
SoundType.Audio -> pack loading, audio processing, mixer, EQ, playback
tools           -> pack validation/import and portable publishing
assets/packs    -> built-in sourced sound packs
```

## Start Here

| Task | Best starting point |
| --- | --- |
| Build/test | `docs/DEVELOPMENT.md` |
| Package/release | `docs/PACKAGING.md` and `tools/publish-portable.ps1` |
| Sound pack format | `docs/SOUND_PACK_FORMAT.md` |
| Bundled sample provenance | `docs/SAMPLE_PROVENANCE.md` and pack-local source files |
| Third-party notices for users | `THIRD_PARTY_NOTICES.md` |
| Reference implementation notes | `docs/REFERENCE_NOTES.md` |
| Privacy claims | `docs/PRIVACY.md` |
| Manual Windows QA | `docs/QA_CHECKLIST.md` |
| Current product direction | `docs/ROADMAP.md` |
| Historical first-build scope | `docs/archive/BUILD_SPEC.md` |

## Docs Map

Live maintainer docs live in `docs/`. Treat `docs/archive/` as historical
context unless a current doc explicitly points there.

```text
README.md                    -> Product overview and download path
AGENTS.md                    -> Agent-specific packaging and debugging rules
docs/REPO_GUIDE.md           -> This orientation map
docs/DEVELOPMENT.md          -> Local SDK, project boundaries, verification
docs/PACKAGING.md            -> Portable publish output and release zip shape
docs/QA_CHECKLIST.md         -> Manual Windows smoke checks
docs/SOUND_PACK_FORMAT.md    -> Folder and archive pack contracts
docs/SAMPLE_PROVENANCE.md    -> Maintainer-facing bundled sample sources
docs/REFERENCE_NOTES.md      -> Reference app/audio ideas and adaptation notes
docs/PRIVACY.md              -> User-facing privacy statement
docs/ROADMAP.md              -> Current product direction
THIRD_PARTY_NOTICES.md       -> User-facing attribution and license notices
```

## Running And Packaging

For source-level debugging:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\src\SoundType.App\SoundType.App.csproj
```

For a launchable portable build:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-portable.ps1
```

The direct local launch target is:

```text
artifacts\publish\SoundType\SoundType.exe
```

The release upload artifacts are still only:

```text
artifacts\SoundType-win-x64-Release-portable.zip
artifacts\SoundType-win-x64-Release-portable.sha256
```

## Navigation Notes

- `MainWindow.xaml` is still the large WPF layout file. Keep behavior changes small and verified.
- App display-only list items live in `src/SoundType.App/ViewModels`.
- Shared C# project defaults live in `Directory.Build.props`; project files only
  keep settings that are specific to that project.
- Do not commit `.tools`, `.external`, `.private`, `artifacts`, `bin`, or `obj`.
- `artifacts\publish\SoundType\SoundType.exe` is useful local output, not a committed artifact.
- Keep release screenshots under `artifacts\screenshots` and temporary UI checks under `artifacts\ui-qa`.

## Hotspots

These are the files future cleanup should approach carefully:

| Path | Why it matters |
| --- | --- |
| `src/SoundType.App/MainWindow.xaml` | Large WPF shell; resources and page bodies are still inline. Split resource dictionaries or page controls only with build/test coverage. |
| `src/SoundType.App/MainWindow.xaml.cs` | Main orchestration point for startup, audio selection, tray, hotkeys, navigation, rules, settings, and waveform refresh. Prefer small services or manual partials before adding more responsibilities. |
| `src/SoundType.App/ViewModels/AppVisual.cs` | Display model that also resolves native app icons. If icon behavior grows, move interop into an app service and keep the model plain. |
| `src/SoundType.Audio/AudioEngine.cs` | Main playback runtime path. Changes here affect latency, overlapping playback, output-device handling, and cache pruning. |
| `src/SoundType.Tests/ReleaseReadinessTests.cs` | String-based release guardrails. Useful, but update them deliberately when moving XAML or release docs. |
