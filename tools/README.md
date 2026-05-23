# SoundType Tools

The tools folder holds developer utilities for sound packs and release packaging.

## Scripts

| Script | Purpose |
| --- | --- |
| `generate-original-sound-packs.ps1` | Legacy/dev fallback for generated WAV packs; current bundled packs favor sourced profiles |
| `generate-placeholder-sounds.ps1` | Compatibility wrapper that calls the legacy generated-pack script |
| `publish-portable.ps1` | Builds the Windows x64 portable output into `artifacts` |

## Console Tools

| Tool | Purpose |
| --- | --- |
| `SoundType.PackValidator` | Validates folder packs, `.zip`, or `.soundpack` archives |
| `SoundType.MechvibesImporter` | Converts compatible Mechvibes folder packs into SoundType folder packs |

Run with the local SDK:

```powershell
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.PackValidator\SoundType.PackValidator.csproj -- .\assets\packs\Freesound-Gate13Actions
.\.tools\dotnet\dotnet.exe run --project .\tools\SoundType.MechvibesImporter\SoundType.MechvibesImporter.csproj -- C:\source\pack .\dist\ConvertedPack
```

Only redistribute third-party converted packs when their original license allows it.

`publish-portable.ps1` leaves a direct local launch target at `artifacts\publish\SoundType\SoundType.exe` and also writes the release zip plus checksum beside it.
