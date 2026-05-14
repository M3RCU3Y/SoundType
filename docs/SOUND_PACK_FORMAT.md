# Sound Pack Format

A SoundType sound pack is a folder with a `pack.json` file and WAV assets. A `.soundpack` file is a zip archive that wraps the same folder-compatible structure.

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

Use the developer validator to check a folder pack:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\assets\packs\ClassicTypewriter
```

Use the same command for a `.soundpack` or `.zip` archive:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\dist\RainyTypewriter.soundpack
```

The validator prints the pack name and id for valid packs. It prints validation errors and exits nonzero for invalid packs.

## Archives

SoundType can export a folder-based pack to a zip archive with the intended `.soundpack` extension. The archive contents use the pack folder as the root, so `pack.json` must be at the archive root rather than inside an extra nested directory.

SoundType can import `.soundpack` and `.zip` archives into the packs root. Import extracts to a temporary staging folder, validates the staged folder with the same folder-pack validation, then installs it into a folder named after the pack `id`.

The validator also imports archives into a temporary validation folder, validates the imported metadata and files there, then removes the temporary folder. It does not write validated archive contents into `assets/packs`.

When `overwrite` is disabled, import refuses to replace an existing pack folder with the same `id`. When `overwrite` is enabled, SoundType replaces only that pack's target folder after the archive has extracted and validated successfully.

Archive entries are rejected if they would extract outside the staging folder, including `../` path traversal and absolute paths.
