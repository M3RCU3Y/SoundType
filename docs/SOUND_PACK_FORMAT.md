# Sound Pack Format

A SoundType sound pack is currently a folder with a `pack.json` file and WAV assets. Future `.soundpack` zip import/export support can wrap this same structure.

## Required

- `pack.json`
- At least one `.wav` file in the `normal` group

## Optional

- `enter`, `space`, `backspace`, and `tab` groups
- Preview image
- Key overrides
- Default volume
- Random sample selection

## Example

```json
{
  "id": "rainy-typewriter",
  "name": "Rainy Typewriter",
  "author": "Mercury",
  "version": "1.0.0",
  "description": "Soft vintage typewriter sounds with a warm enter ding.",
  "license": "Personal use",
  "groups": {
    "normal": [
      "normal/key01.wav",
      "normal/key02.wav"
    ],
    "enter": [
      "enter/ding.wav"
    ]
  },
  "keyOverrides": {
    "Enter": "enter"
  },
  "defaults": {
    "volume": 0.75,
    "pitchVariation": 0.0,
    "randomize": true
  }
}
```

## Validation

SoundType validates that `pack.json` can be parsed, `id` and `name` exist, the `normal` group exists, all listed audio files exist, and all listed audio files are `.wav`.
