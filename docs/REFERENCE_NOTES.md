# Reference Notes

SoundType's WPF theme polish uses Keyboard Sounds Pro as a conceptual visual reference. The adapted UI ideas are theme-level treatment: dark elevated cards, subtle borders and shadows, emerald accent states, and distinct disabled/error/warning colors. No React, CSS, Go, or Wails implementation was copied into SoundType.

SoundType also adapts backend/audio concepts from Keyboard Sounds Pro into native .NET code:

- preload decoded audio into memory before playback;
- use non-blocking overlapping sample playback instead of restarting one shared sample cursor;
- support KSP-style key release samples through optional `*-release` sound groups;
- expose 10-band equalizer controls at 60, 170, 310, 600, 1k, 3k, 6k, 12k, 14k, and 16k Hz;
- support stereo panning by key position or randomized placement;
- render waveform previews from decoded sample peaks.

Reference reviewed:

- Keyboard Sounds Pro, MIT License, copyright 2025 Nathan Fiscaletti.
- Mechvibes, MIT License, copyright 2021 Hai Nguyen. This branch did not adapt Mechvibes implementation details.

SoundType keeps its existing WPF architecture and control flow. The styling and audio changes live in WPF resource dictionaries, native controls, and SoundType's C# audio pipeline.
