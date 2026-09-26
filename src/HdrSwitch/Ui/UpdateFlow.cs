using System.Diagnostics;
using HdrSwitch.Core.Localization;
using HdrSwitch.Core.Updates;

namespace HdrSwitch.Ui;

/// <summary>
/// Check for a new release and install it in one click.
///
/// check → "HDR Switch 1.2.0 is available" [Update now] → download + SHA-256 verify → swap the
/// exe in place → start the new version → exit. The new version waits for this one to exit,
/// cleans up and says "Updated to 1.2.0".
///
/// Only ever runs because the user asked: there is no background or scheduled check, so HDR
/// Switch makes no network connection unless someone clicks "Check for updates".
///
/// Runs on the UI thread; the network work is awaited, so the tray stays responsive.
/// </summary>
internal sealed class UpdateFlow : IDisposable
{
    private const int NoticeSeconds = 8;

    private readonly Action _exitApplication;

    private HttpClient? _client;
    private bool _busy;

    internal UpdateFlow(Action exitApplication) => _exitApplication = exitApplication;

    internal static string CurrentVersionText => UpdateChecker.CurrentVersion.ToString(3);

    /// <summary>
    /// A self-contained single-file build can replace itself. A `dotnet run` or framework-dependent
    /// build cannot sensibly, so it is sent to the release page instead.
    /// </summary>
    private static bool CanSelfUpdate =>
        Environment.ProcessPath is { } exe &&
        string.Equals(Path.GetFileName(exe), UpdateChecker.ExecutableAsset, StringComparison.OrdinalIgnoreCase) &&
        !File.Exists(Path.Combine(AppContext.BaseDirectory, "HdrSwitch.dll"));

    internal static void AnnounceUpdated() =>
        ToastWindow.ShowNotice(
            L.F("HDR Switch updated to {0}", CurrentVersionText),
            L.T("Your settings and learned rules carried over."),
            NoticeSeconds,
            actionText: L.T("What's new"),
            onAction: () => OpenUrl(UpdateChecker.ReleasesPage));

    /// <summary>The tray menu / Settings button. Always reports a result, including "up to date".</summary>
    internal async Task CheckNowAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            ReleaseInfo? release;
            try
            {
                release = await UpdateChecker.GetLatestAsync(Client);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                ToastWindow.ShowNotice(L.T("Could not check for updates"), Describe(ex), NoticeSeconds);
                return;
            }

            if (release is null || !UpdateChecker.IsNewer(release, UpdateChecker.CurrentVersion))
            {
                ToastWindow.ShowNotice(
                    L.T("HDR Switch is up to date"),
                    L.F("You have version {0}, the latest release.", CurrentVersionText),
                    NoticeSeconds - 3);
                return;
            }

            OfferUpdate(release);
        }
        finally
        {
            _busy = false;
        }
    }

    private void OfferUpdate(ReleaseInfo release)
    {
        var version = release.Version.ToString(3);

        if (!CanSelfUpdate)
        {
            ToastWindow.ShowNotice(
                L.F("HDR Switch {0} is available", version),
                L.F("You have {0}. This build cannot update itself; download the new one from the release page.", CurrentVersionText),
                20,
                actionText: L.T("Open release page"),
                onAction: () => OpenUrl(release.PageUrl));
            return;
        }

        ToastWindow.ShowNotice(
            L.F("HDR Switch {0} is available", version),
            DescribeUpdate(release),
            30,
            actionText: L.T("Update now"),
            onAction: () => _ = InstallAsync(release));
    }

    /// <summary>"What's new" first, when the release notes have it; then what clicking will do.</summary>
    internal static string DescribeUpdate(ReleaseInfo release)
    {
        var lines = new List<string>();

        if (release.Highlights.Count > 0)
        {
            lines.Add(L.T("What's new:"));
            lines.AddRange(release.Highlights.Select(h => "•  " + h));
            lines.Add(string.Empty);
        }

        lines.Add(L.F("You have {0}. Updating downloads the new version, checks it against the published SHA-256 and restarts, in a few seconds.", CurrentVersionText));

        return string.Join(Environment.NewLine, lines);
    }

    private async Task InstallAsync(ReleaseInfo release)
    {
        if (_busy || Environment.ProcessPath is not { } exePath)
        {
            return;
        }

        _busy = true;
        var version = release.Version.ToString(3);
        var progress = ToastWindow.ShowNotice(L.F("Updating to {0}…", version), L.T("Downloading."), 120);

        try
        {
            var downloaded = await UpdateInstaller.DownloadAsync(Client, release, exePath);
            UpdateInstaller.Swap(exePath, downloaded);
            progress.Dismiss();

            try
            {
                using var started = Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--after-update {Environment.ProcessId}",
                    UseShellExecute = false,
                });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // The new exe is already in place; only the restart failed.
                ToastWindow.ShowNotice(
                    L.F("Updated to {0} — restart to finish", version),
                    L.F("The new version is installed but did not start ({0}). Exit HDR Switch and start it again.", ex.Message),
                    20);
                return;
            }

            _exitApplication();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            progress.Dismiss();

            var detail = ex is UnauthorizedAccessException
                ? L.F("HDR Switch cannot write to {0}. Move HdrSwitch.exe to a folder you own (for example Documents or Desktop), or download the update by hand.", Path.GetDirectoryName(exePath))
                : Describe(ex);

            ToastWindow.ShowNotice(
                L.T("Update failed — nothing was changed"),
                detail,
                20,
                actionText: L.T("Open release page"),
                onAction: () => OpenUrl(release.PageUrl));
        }
        finally
        {
            _busy = false;
        }
    }

    private HttpClient Client => _client ??= UpdateChecker.CreateHttpClient();

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException => L.T("GitHub did not answer in time. Check your connection and try again."),
        HttpRequestException { StatusCode: { } code } => L.F("GitHub answered {0} {1}.", (int)code, code),
        HttpRequestException => L.T("Could not reach GitHub. Check your connection and try again."),
        _ => ex.Message,
    };

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ToastWindow.ShowNotice(L.T("Could not open the browser"), url, NoticeSeconds);
        }
    }

    public void Dispose() => _client?.Dispose();
}
