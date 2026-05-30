# Enter Ding Sample Source Notes

This note is intentionally untracked. It records where the Enter Ding samples in
`assets/packs/SoundType-EnterDing` came from, based on the local source-of-truth
files already in the repo.

## Local Files Checked

- `assets/packs/SoundType-EnterDing/pack.json`
- `assets/packs/SoundType-EnterDing/SOURCE.txt`
- `THIRD_PARTY_NOTICES.md`
- `docs/SAMPLE_PROVENANCE.md`

## Pack

- Pack id: `soundtype-enter-ding`
- Pack name: `SoundType Enter Ding`
- Version: `1.2.0`
- Local folder: `assets/packs/SoundType-EnterDing`
- Shipped groups:
  - `normal`: `ding-01.wav` through `ding-12.wav`
  - `enter`: `ding-01.wav` through `ding-12.wav`
  - individual groups: `ding-01` through `ding-12`

The `normal` and `enter` folders contain matching copies of the twelve ding
variants. The app exposes them in Settings as:

- `ding-01`: Classic Typewriter Bell
- `ding-02`: Bright Margin Bell
- `ding-03`: Antique Return Bell
- `ding-04`: Warm Carriage Bell
- `ding-05`: Clean Line Bell
- `ding-06`: Tiny Line Bell
- `ding-07`: Reward Tap Bell
- `ding-08`: Golden Margin Bell
- `ding-09`: Pleasing Star Bell
- `ding-10`: Warm Desk Bell
- `ding-11`: Round Desk Bell
- `ding-12`: Deep Typewriter Bell

## Source Recordings Listed Locally

`assets/packs/SoundType-EnterDing/SOURCE.txt` lists these source recordings:

- `https://freesound.org/people/ramsamba/sounds/318687/`
  - Local note: `Typewriter Bell.wav`
- `https://freesound.org/s/318686/`
  - Local note: `Typewriter Carriage Return.wav`
- `https://freesound.org/people/knufds/sounds/345955/`
  - Local note: `Typewriter bell & carriage reset`
- `https://freesound.org/people/MasterNavigator/sounds/444813/`
  - Local note: `typewriter bell`
- `https://opengameart.org/content/point-bell`
- `https://opengameart.org/content/bell-dingschimes`
- `https://opengameart.org/content/pleasing-bell-sound-effect`
  - Local note: `pleasing-bell.wav`
- `https://opengameart.org/content/correct-bell`
  - Local note: `bell.wav`

The newer variants are processed from OpenGameArt downloads plus derivatives of
the classic typewriter bell:

- `sd_0.wav`
- `pleasing-bell.wav`
- `bell.wav`

## License / Provenance Note

The local pack metadata says:

- `license`: `Processed CC0 source recordings. See SOURCE.txt and THIRD_PARTY_NOTICES.md.`

The local `SOURCE.txt` says:

- License: Creative Commons 0 / public domain source recordings.

`THIRD_PARTY_NOTICES.md` also records the same Enter Ding source links and says
the bundled WAV files are trimmed, filtered, mixed, and normalized derivatives
for low-latency playback. The classic typewriter bell derivative keeps a longer
residual tail so it can decay naturally instead of cutting off after the initial
strike.

## Processing Note

The local source note says the source recordings were:

- converted to mono 44.1 kHz 16-bit WAV
- trimmed for short Enter overlays
- high/low-pass filtered
- lightly mixed with a typewriter carriage transient
- normalized with limiter headroom
- faded in/out to avoid clicks, crackle, clipping, repeated-tail buildup, or
  harsh ringing

## Confidence

This document is based on local repository records, not a fresh web re-check of
each source page. The repo-local attribution trail is internally consistent:
`pack.json`, `SOURCE.txt`, `THIRD_PARTY_NOTICES.md`, and
`docs/SAMPLE_PROVENANCE.md` all point to the same provenance story.
