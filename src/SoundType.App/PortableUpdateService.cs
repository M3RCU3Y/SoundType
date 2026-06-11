using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace SoundType.App;

internal sealed class PortableUpdateService
{
    public async Task StartUpdateAsync(
        string portableZipUrl,
        string portableChecksumUrl,
        string releaseUrl)
    {
        string workDir = Path.Combine(Path.GetTempPath(), "SoundTypeUpdate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        string zipPath = Path.Combine(workDir, "SoundType-update.zip");
        string scriptPath = Path.Combine(workDir, "Install-SoundTypeUpdate.ps1");

        using HttpClient client = new();
        await using (Stream download = await client.GetStreamAsync(portableZipUrl))
        await using (FileStream zip = File.Create(zipPath))
        {
            await download.CopyToAsync(zip);
        }

        string checksumText = await client.GetStringAsync(portableChecksumUrl);
        VerifyDownloadedUpdate(zipPath, checksumText);
        await File.WriteAllTextAsync(scriptPath, BuildPortableUpdateScript());

        string exePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? Path.Combine(AppContext.BaseDirectory, "SoundType.exe");
        string installDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        ProcessStartInfo startInfo = new()
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ZipPath \"{zipPath}\" -InstallDir \"{installDir}\" -ExePath \"{exePath}\" -ProcessId {Environment.ProcessId} -WorkDir \"{workDir}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workDir
        };

        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException($"Could not start the SoundType updater. Download the update manually from {releaseUrl}.");
        }
    }

    private static void VerifyDownloadedUpdate(string zipPath, string checksumText)
    {
        string expectedHash = checksumText
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "";
        if (expectedHash.Length != 64)
        {
            throw new InvalidOperationException("The update checksum is invalid.");
        }

        using FileStream zip = File.OpenRead(zipPath);
        string actualHash = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The update download did not match its checksum.");
        }
    }

    private static string BuildPortableUpdateScript() =>
        """
        param(
            [Parameter(Mandatory=$true)][string]$ZipPath,
            [Parameter(Mandatory=$true)][string]$InstallDir,
            [Parameter(Mandatory=$true)][string]$ExePath,
            [Parameter(Mandatory=$true)][int]$ProcessId,
            [Parameter(Mandatory=$true)][string]$WorkDir
        )

        $ErrorActionPreference = "Stop"
        Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue

        $extractDir = Join-Path $WorkDir "extracted"
        if (Test-Path -LiteralPath $extractDir) {
            Remove-Item -LiteralPath $extractDir -Recurse -Force
        }

        New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
        Expand-Archive -LiteralPath $ZipPath -DestinationPath $extractDir -Force

        $newExe = Get-ChildItem -LiteralPath $extractDir -Filter "SoundType.exe" -Recurse -File | Select-Object -First 1
        if (-not $newExe) {
            throw "The update package does not contain SoundType.exe."
        }

        $sourceDir = $newExe.Directory.FullName
        Get-ChildItem -LiteralPath $InstallDir -Force |
            Where-Object { $_.FullName -ne $WorkDir } |
            Remove-Item -Recurse -Force

        Copy-Item -Path (Join-Path $sourceDir "*") -Destination $InstallDir -Recurse -Force
        Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir
        """;
}
