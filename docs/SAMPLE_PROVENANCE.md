# Sound Pack Sample Provenance

This document is a maintainer reference for where the bundled SoundType samples came from. It is intentionally based on the local source-of-truth files committed with each pack:

- `assets/packs/*/pack.json`
- `assets/packs/*/LICENSE`
- `assets/packs/*/SOURCE.txt`
- `assets/packs/*/SOURCE_README.txt`
- `assets/packs/*/SOURCE_CONFIG.json`
- `THIRD_PARTY_NOTICES.md`

When adding or replacing samples, update the pack-local source file first, then update this summary.

## Summary by Source Family

| Source family | Bundled folders | License in repo | Local proof files |
| --- | --- | --- | --- |
| KeyboardSounds Pro / kbsim | `assets/packs/KSP-*` | MIT | `pack.json`, `LICENSE` |
| Mechvibes | `assets/packs/Mechvibes-*` | MIT | `pack.json`, `LICENSE`, `SOURCE_CONFIG.json` |
| Freesound typewriter recordings | `assets/packs/Freesound-*` | CC0 1.0 | `pack.json`, `SOURCE.txt` |
| Chosic typewriter recording | `assets/packs/Chosic-TypewriterDesk` | Creative Commons CC0 Public Domain | `pack.json`, `SOURCE.txt` |
| OpenGameArt keyboard/typewriter sounds | `assets/packs/OGACherryKC1000`, `assets/packs/OpenGameArt-BMacTypewriter` | CC0 | `pack.json`, `SOURCE_README.txt` or `SOURCE.txt` |
| SoundType Enter ding overlays | `assets/packs/SoundType-EnterDing` | Processed CC0 source recordings | `pack.json`, `SOURCE.txt`, `THIRD_PARTY_NOTICES.md` |

## Pack-by-Pack Provenance

| Folder | Pack name | Sample source | License / provenance note | Local files to inspect |
| --- | --- | --- | --- | --- |
| `Chosic-TypewriterDesk` | Typewriter Desk | Chosic download `https://www.chosic.com/download-audio/54458/` | Creative Commons CC0 Public Domain. The source recording was converted to mono 44.1 kHz WAV, filtered, segmented into one-shots, normalized, and faded for key playback. | `pack.json`, `SOURCE.txt` |
| `Freesound-CloseVintage` | Close Vintage Typewriter | Freesound sound `801118` by `nvmbky` | CC0 1.0. The source recording was converted to mono 44.1 kHz WAV, filtered, segmented into one-shots, normalized, and faded for key playback. | `pack.json`, `SOURCE.txt` |
| `Freesound-Gate13Actions` | Gate13 Typewriter Actions | Freesound sound `697389` by `Gate13` | CC0 1.0. The source recording was converted to mono 44.1 kHz WAV, filtered, segmented into one-shots, normalized, and faded for key playback. | `pack.json`, `SOURCE.txt` |
| `Freesound-OlivettiCollege` | Olivetti College | Freesound sound `770623` by `Spacekittycat` | CC0 1.0. The source recording was converted to mono 44.1 kHz WAV, filtered, segmented into compact key hits, normalized, and faded. | `pack.json`, `SOURCE.txt` |
| `Freesound-WW2Typewriter` | WW2 Typewriter | Freesound sound `164807` by `exterminat` | CC0 1.0. Slow and fast typing passes were cut into tight one-shots. | `pack.json`, `SOURCE.txt` |
| `KSP-Alpaca` | Alpaca Switches | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/alpaca`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-GateronBlackInk` | Gateron Black Ink | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/gateron-black-ink`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-GateronRedInk` | Gateron Red Ink | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/gateron-red-ink`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-HolyPanda` | Holy Panda | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/holy-panda`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-LogitechG915Brown` | Logitech G915 TKL Brown | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/logitech-g915-tkl-brown` | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-MXBlack` | Cherry MX Black | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/mx-black`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-MXBlue` | Cherry MX Blue | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/mx-blue`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-MXBrown` | Cherry MX Brown | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/mx-brown`; license file names `https://github.com/tplai/kbsim` as the audio sample origin | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-NKCream` | NovelKeys Cream | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/nk-cream` | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `KSP-OperaGX` | Opera GX Keyboard | KeyboardSounds Pro bundled profile `desktop/bundled-profiles/opera-gx` | MIT. Preserve the bundled `LICENSE`. | `pack.json`, `LICENSE` |
| `Mechvibes-CreamFullTravel` | Cream Full Travel | Mechvibes `src/audio/cream-travel` at commit `b7cb633`; original Mechvibes config preserved | MIT. Preserve `LICENSE` and `SOURCE_CONFIG.json`. | `pack.json`, `LICENSE`, `SOURCE_CONFIG.json` |
| `Mechvibes-MXBlackFullTravel` | MX Black Full Travel | Mechvibes `src/audio/mxblack-travel` at commit `b7cb633`; original Mechvibes config preserved | MIT. Preserve `LICENSE` and `SOURCE_CONFIG.json`. | `pack.json`, `LICENSE`, `SOURCE_CONFIG.json` |
| `Mechvibes-MXBlueFullTravel` | MX Blue Full Travel | Mechvibes `src/audio/mxblue-travel` at commit `b7cb633`; original Mechvibes config preserved | MIT. Preserve `LICENSE` and `SOURCE_CONFIG.json`. | `pack.json`, `LICENSE`, `SOURCE_CONFIG.json` |
| `Mechvibes-MXBrownFullTravel` | MX Brown Full Travel | Mechvibes `src/audio/mxbrown-travel` at commit `b7cb633`; original Mechvibes config preserved | MIT. Preserve `LICENSE` and `SOURCE_CONFIG.json`. | `pack.json`, `LICENSE`, `SOURCE_CONFIG.json` |
| `Mechvibes-TurquoiseFullTravel` | Turquoise Full Travel | Mechvibes `src/audio/turquoise` at commit `b7cb633`; original Mechvibes config preserved | MIT. Preserve `LICENSE` and `SOURCE_CONFIG.json`. | `pack.json`, `LICENSE`, `SOURCE_CONFIG.json` |
| `OGACherryKC1000` | Cherry KC 1000 Real Keys | OpenGameArt keyboard soundpack `https://opengameart.org/content/keyboard-soundpack-1-typing-and-single-keystrokes` by `unicaegames` | CC0. Source README says the asset includes human typing sounds, generated sounds, and 32 single keypress sounds; SoundType bundles the single keypress recordings. | `pack.json`, `SOURCE_README.txt` |
| `OpenGameArt-BMacTypewriter` | BMacZero Typewriter Tap | OpenGameArt Mechanical Sounds `https://opengameart.org/content/mechanical-sounds` by `BMacZero` | CC0. The source was converted to mono 44.1 kHz WAV, filtered, segmented into one-shots, normalized, and faded. | `pack.json`, `SOURCE.txt` |
| `SoundType-EnterDing` | SoundType Enter Ding | CC0/public-domain Freesound and OpenGameArt bell/chime recordings listed in `SOURCE.txt` | Processed CC0 source recordings. Converted to mono 44.1 kHz WAV, trimmed, filtered, mixed, normalized with limiter headroom, and faded for short Enter overlays. | `pack.json`, `SOURCE.txt`, `THIRD_PARTY_NOTICES.md` |

## SoundType Enter Ding Source List

`assets/packs/SoundType-EnterDing/SOURCE.txt` lists these source recordings:

- `https://freesound.org/people/ramsamba/sounds/318687/`
- `https://freesound.org/s/318686/`
- `https://freesound.org/people/knufds/sounds/345955/`
- `https://freesound.org/people/MasterNavigator/sounds/444813/`
- `https://opengameart.org/content/point-bell`
- `https://opengameart.org/content/bell-dingschimes`

## Maintenance Notes

- Keep `THIRD_PARTY_NOTICES.md` user-facing and concise.
- Keep this file maintainer-facing and specific enough to answer "where did this bundled sample come from?"
- Do not rely only on this summary for licensing. The pack-local `LICENSE`, `SOURCE.txt`, `SOURCE_README.txt`, and `SOURCE_CONFIG.json` files are the stronger source of truth.
- If a pack is renamed, update both the folder name in this document and the corresponding `pack.json` entry.
