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
| Privacy claims | `docs/PRIVACY.md` |
| Manual Windows QA | `docs/QA_CHECKLIST.md` |
| Current product direction | `docs/ROADMAP.md` |
| Historical first-build scope | `docs/archive/BUILD_SPEC.md` |

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
- Do not commit `.tools`, `.external`, `.private`, `artifacts`, `bin`, or `obj`.
- `artifacts\publish\SoundType\SoundType.exe` is useful local output, not a committed artifact.
- Keep release screenshots under `artifacts\screenshots` and temporary UI checks under `artifacts\ui-qa`.
