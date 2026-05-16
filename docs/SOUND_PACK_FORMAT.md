# Sound Pack Format

A SoundType sound pack is a folder with a `pack.json` file and WAV or MP3 assets. A `.soundpack` file is a zip archive that wraps the same folder-compatible structure.

## Required

- `pack.json`
- At least one `.wav` or `.mp3` file in the `normal` group

## Optional

- `enter`, `space`, `backspace`, and `tab` groups
- Preview image
- Tags for library grouping, such as `switch`, `typewriter`, `clicky`, or `thock`
- Key overrides
- Default volume
- Random sample selection and pitch variation

## Example

```json
{
  "id": "rainy-typewriter",
  "name": "Rainy Typewriter",
  "author": "Mercury",
  "version": "1.0.0",
  "description": "Soft vintage typewriter sounds with a warm enter ding.",
  "license": "Personal use",
  "previewImage": "preview.png",
  "tags": [ "typewriter", "vintage" ],
  "groups": {
    "normal": [
      "normal/key01.wav",
      "normal/key02.mp3"
    ],
    "enter": [
      "enter/ding.mp3"
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

SoundType validates that `pack.json` can be parsed, `id` and `name` exist, the `normal` group exists, all listed audio files exist, and all listed audio files are `.wav` or `.mp3`.

OGG files are not supported with the current NAudio dependency set. Packs that list `.ogg` samples are rejected with a supported-formats validation error instead of being partially imported.

Use the developer validator to check a folder pack:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\assets\packs\Freesound-RoyalQuietDeluxe
```

Use the same command for a `.soundpack` or `.zip` archive:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\dist\MyPack.soundpack
```

The validator prints the pack name and id for valid packs. It prints validation errors and exits nonzero for invalid packs.

## Archives

SoundType can export a folder-based pack to a zip archive with the intended `.soundpack` extension. The archive contents use the pack folder as the root, so `pack.json` must be at the archive root rather than inside an extra nested directory.

SoundType can import `.soundpack` and `.zip` archives into the packs root. Import extracts to a temporary staging folder, validates the staged folder with the same folder-pack validation, then installs it into a folder named after the pack `id`.

The validator also imports archives into a temporary validation folder, validates the imported metadata and files there, then removes the temporary folder. It does not write validated archive contents into `assets/packs`.

When `overwrite` is disabled, import refuses to replace an existing pack folder with the same `id`. When `overwrite` is enabled, SoundType replaces only that pack's target folder after the archive has extracted and validated successfully.

Archive entries are rejected if they would extract outside the staging folder, including `../` path traversal and absolute paths.

## Mechvibes Conversion

The developer importer can convert a Mechvibes folder pack into the SoundType folder format:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.MechvibesImporter\SoundType.MechvibesImporter.csproj -- C:\path\to\mechvibes-pack .\dist\ConvertedPack
```

Use `--overwrite` as the third argument when the output folder already contains files:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.MechvibesImporter\SoundType.MechvibesImporter.csproj -- C:\path\to\mechvibes-pack .\dist\ConvertedPack --overwrite
```

Supported source shape:

- Folder pack with `config.json`.
- `key_define_type: "multi"` where `sound` and `defines` point at individual sample files.
- `.wav` and `.mp3` samples in the generated SoundType manifest.

Mapping behavior:

- Mechvibes `sound` becomes SoundType `normal`.
- Mechvibes key codes `14`, `28`, `57`, and `15` map to `backspace`, `enter`, `space`, and `tab`.
- Other key-down definitions map to `normal`.
- `*-up` release definitions are skipped because SoundType currently plays key-down sounds.
- Each referenced WAV/MP3 sample is copied once into `samples/`, even if multiple keys point at it.

Unsupported source shape:

- `key_define_type: "single"` sprite packs are rejected with an error because conversion would require extracting timed slices from one audio file.
- `.ogg` and other unsupported references are skipped with warnings when playable WAV/MP3 samples remain. If no supported normal WAV/MP3 samples remain, conversion fails.

Licensing caveat: Mechvibes packs may include third-party recordings with their own terms. Convert and redistribute only when the original license allows it; the generated `pack.json` includes a reminder rather than a rights grant.
