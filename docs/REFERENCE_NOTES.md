# Reference Notes

SoundType's WPF theme polish uses Keyboard Sounds Pro as a conceptual visual reference. The adapted UI ideas are theme-level treatment: dark elevated cards, subtle borders and shadows, emerald accent states, and distinct disabled/error/warning colors. No React, CSS, Go, or Wails implementation was copied into SoundType.

SoundType also adapts backend/audio concepts from Keyboard Sounds Pro into native .NET code:

- preload decoded audio into memory before playback;
- use non-blocking overlapping sample playback instead of restarting one shared sample cursor;
- support KSP-style key release samples through optional `*-release` sound groups;
- keep default pitch variation at 0 so sourced recordings are not resampled unless the user opts in;
- keep Mechvibes full-travel packs in per-file `mp3` form to reuse their press/release mapping without adding runtime codec weight;
- extract long CC0 typewriter recordings into short one-shot clips before bundling so each key press stays low-latency and does not start a long background typing loop;
- expose 10-band equalizer controls at 60, 170, 310, 600, 1k, 3k, 6k, 12k, 14k, and 16k Hz;
- support stereo panning by key position or randomized placement;
- render waveform previews from decoded sample peaks.

Reference reviewed:

- Keyboard Sounds Pro, MIT License, copyright 2025 Nathan Fiscaletti.
- Mechvibes, MIT License, copyright 2021 Hai Nguyen. SoundType imports selected sound profiles only; no Mechvibes implementation code is copied.
- BigSoundBank Hermes Precisa 305 typewriter recordings, CC0/public-domain equivalent, by Joseph SARDIN. SoundType imports extracted one-shot clips only.
- Freesound CC0 typewriter recordings by videog, exterminat, TOC1, nvmbky, Spacekittycat, Gate13, cabled_mess, and bubblegump1977. SoundType imports preview/download audio as segmented one-shot clips with per-pack `SOURCE.txt` files.
- Chosic Typewriter, Creative Commons CC0 Public Domain, imported as segmented one-shot clips.
- OpenGameArt Mechanical Sounds by BMacZero, CC0, imported as a short typewriter tap pack.

SoundType keeps its existing WPF architecture and control flow. The styling and audio changes live in WPF resource dictionaries, native controls, and SoundType's C# audio pipeline.
