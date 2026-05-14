# Packaging

SoundType currently ships as a portable Windows zip. It does not produce an installer, MSIX package, or auto-updater yet.

## Requirements

- Windows 10/11.
- .NET 8 SDK.
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

The script publishes `src\SoundType.App\SoundType.App.csproj` to:

```text
artifacts\publish\SoundType
```

It then creates the portable archive:

```text
artifacts\SoundType-win-x64-Release-portable.zip
```

The zip contains `SoundType.App.exe`, runtime dependencies emitted by `dotnet publish`, and assets copied by the project file, including the built-in sound packs.

## Current Limitation

This package is a portable folder zip only. Users extract the archive and run `SoundType.App.exe`. A signed installer, MSIX package, desktop shortcuts, and update flow are future release work.
