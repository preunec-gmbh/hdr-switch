using System.Security.Cryptography;

namespace HdrSwitch.Core.Updates;

/// <summary>
/// Replaces the running HdrSwitch.exe with a downloaded release.
///
/// There is no installer, so the update is a file swap next to the executable:
/// <list type="number">
///   <item>download to <c>HdrSwitch.exe.download</c> in the same folder (same volume, so the
///   final step is a rename, and an unwritable folder fails before anything is touched);</item>
///   <item>verify it against the release's published SHA-256;</item>
///   <item>rename the running exe to <c>HdrSwitch.exe.old</c> -- Windows allows renaming a
///   running executable, just not overwriting it -- and move the download into its place;</item>
///   <item>the caller starts the new exe and exits; the new process deletes the <c>.old</c>.</item>
/// </list>
/// The path stays the same, so the Start-with-Windows entry and any shortcuts keep working.
/// </summary>
public static class UpdateInstaller
{
    public const string DownloadSuffix = ".download";
    public const string OldSuffix = ".old";

    /// <summary>A release binary is ~48 MB; refuse anything absurd rather than fill the disk.</summary>
    private const long MaxDownloadBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Downloads and verifies the release next to <paramref name="exePath"/>. Returns the path
    /// of the verified file. Throws on any failure, leaving no partial download behind.
    /// </summary>
    public static async Task<string> DownloadAsync(
        HttpClient client,
        ReleaseInfo release,
        string exePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var expected = ParseChecksum(
            await client.GetStringAsync(release.ChecksumUrl, cancellationToken).ConfigureAwait(false),
            UpdateChecker.ExecutableAsset)
            ?? throw new InvalidDataException("The release's checksum file could not be read.");

        var target = exePath + DownloadSuffix;

        try
        {
            using var response = await client.GetAsync(
                release.ExecutableUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            if (total > MaxDownloadBytes)
            {
                throw new InvalidDataException("The download is far larger than a release should be.");
            }

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long written = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    written += read;
                    if (written > MaxDownloadBytes)
                    {
                        throw new InvalidDataException("The download is far larger than a release should be.");
                    }

                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

                    if (total is > 0)
                    {
                        progress?.Report((double)written / total.Value);
                    }
                }
            }

            var actual = await ComputeSha256Async(target, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The downloaded file does not match the release's published SHA-256, so it was discarded.");
            }

            return target;
        }
        catch
        {
            TryDelete(target);
            throw;
        }
    }

    /// <summary>
    /// Puts the verified download in place of <paramref name="exePath"/>. On failure the original
    /// executable is put back, so a failed update never leaves the user without the app.
    /// </summary>
    public static void Swap(string exePath, string downloadedPath)
    {
        var old = exePath + OldSuffix;
        TryDelete(old);

        File.Move(exePath, old);

        try
        {
            File.Move(downloadedPath, exePath);
        }
        catch
        {
            File.Move(old, exePath);
            throw;
        }
    }

    /// <summary>Removes what an earlier update left behind. Safe to call on every start.</summary>
    public static void CleanUp(string exePath)
    {
        TryDelete(exePath + OldSuffix);
        TryDelete(exePath + DownloadSuffix);
    }

    /// <summary>
    /// Reads a sha256sum-style line ("&lt;hex&gt;  HdrSwitch.exe"), or a bare hash. Pure; unit tested.
    /// </summary>
    public static string? ParseChecksum(string content, string fileName)
    {
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            var hash = parts[0];

            if (!IsSha256Hex(hash))
            {
                continue;
            }

            // "*" marks binary mode in sha256sum output.
            var name = parts.Length > 1 ? parts[1].Trim().TrimStart('*') : null;
            if (name is null || string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return hash.ToLowerInvariant();
            }
        }

        return null;
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static bool IsSha256Hex(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still locked by the exiting process, most likely. The next start tries again.
        }
    }
}
