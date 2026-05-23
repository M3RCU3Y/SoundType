# Roadmap

SoundType already has the original prototype milestones: global keyboard hook, tray behavior, settings persistence, built-in sourced packs, import/export, pack validation, app rules, EQ, panning controls, portable packaging, screenshots, and release notes.

## Current Polish

- Keep audio playback centered by default and preserve low-latency key handling.
- Continue trimming `MainWindow.xaml` and `MainWindow.xaml.cs` into clear UI controls, view models, and services when a change naturally touches those areas.
- Keep pack metadata, loudness, and licensing easy to audit.
- Keep the portable package self-contained and easy to launch locally at `artifacts\publish\SoundType\SoundType.exe`.

## Release Readiness

- Add code signing when a certificate is available.
- Add an installer or MSIX only after portable zip releases are stable.
- Expand manual QA coverage on physical Windows machines.
- Keep release assets limited to the portable zip and SHA-256 checksum unless the release process changes.

## Future Product Ideas

- Richer pack marketplace-style browsing.
- More guided sound-pack creation and validation feedback.
- More app-rule presets for games, calls, streaming, and editors.
- Optional update flow after the signed distribution story is settled.
