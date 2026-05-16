# Packaging

SoundType currently ships as a self-contained portable Windows zip. It does not produce an installer, MSIX package, or auto-updater yet.

## Requirements

- Windows 10/11.
- .NET 8 SDK for building from source. End users do not need to install .NET when using the release zip.
- Use `.\.tools\dotnet\dotnet.exe` when a local SDK is available, or `dotnet` from `PATH`.

## Build

```powershell
.\.tools\dotnet\dotnet.exe build .\SoundType.sln -c Release
```

Or, with `dotnet` on `PATH`:

```powershell
dotnet build .\SoundType.sln -c Release
```

## Test

```powershell
.\.tools\dotnet\dotnet.exe test .\SoundType.sln
```

Or, with `dotnet` on `PATH`:

```powershell
dotnet test .\SoundType.sln
```

## Publish Portable Zip

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-portable.ps1
```

The script defaults to `Release` and `win-x64`. Override them when needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\publish-portable.ps1 -Configuration Debug -Runtime win-x64
```

The script publishes `src\SoundType.App\SoundType.App.csproj` as a self-contained Windows x64 app to:

```text
artifacts\publish\SoundType
```

It then creates the portable archive and checksum:

```text
artifacts\SoundType-win-x64-Release-portable.zip
artifacts\SoundType-win-x64-Release-portable.sha256
```

The zip contains `SoundType.exe`, the .NET runtime files emitted by self-contained `dotnet publish`, built-in sound packs, and release trust documents:

- `README.md`
- `PRIVACY.md`
- `PACKAGING.md`
- `THIRD_PARTY_NOTICES.md`

## Current Limitation

This package is a portable folder zip only. Users extract the archive and run `SoundType.exe`. A signed installer, MSIX package, desktop shortcuts, and update flow are future release work. Because SoundType uses a local keyboard hook, public releases should be distributed with the SHA-256 checksum and signed when a signing certificate is available.
