using System.Diagnostics;

namespace HdrSwitch.Core.Sharing;

/// <summary>
/// A deliberately coarse fallback detector, disabled by default.
///
/// The consent store only records apps that capture through Windows.Graphics.Capture. That covers
/// essentially every modern capture app on Windows 11 -- Discord, Chrome, Edge, OBS, Teams and
/// Zoom all register there. Older builds that use legacy DXGI Desktop Duplication or BitBlt do
/// not appear at all.
///
/// For those, the only signal available without a driver is "is this executable running", which
/// cannot distinguish an app that is open from an app that is actually sharing. It is therefore
/// opt-in with an empty default list, and the UI labels it as approximate rather than presenting
/// it as equivalent to the consent-store signal.
/// </summary>
public sealed class ProcessHeuristic
{
    private readonly Func<IReadOnlyList<string>> _runningProcessNames;

    public ProcessHeuristic(Func<IReadOnlyList<string>>? runningProcessNames = null)
    {
        _runningProcessNames = runningProcessNames ?? DefaultRunningProcessNames;
    }

    /// <summary>Suggestions offered in Settings. None of them are enabled unless the user picks them.</summary>
    public static IReadOnlyList<string> SuggestedWatchList { get; } =
    [
        "obs64.exe",
        "obs32.exe",
        "Zoom.exe",
        "CptHost.exe",
        "Teams.exe",
        "Webex.exe",
        "AnyDesk.exe",
        "TeamViewer.exe",
    ];

    public IReadOnlyList<CaptureSession> Detect(IReadOnlyList<string> watchList) =>
        watchList.Count == 0 ? [] : Match(_runningProcessNames(), watchList);

    /// <summary>Pure matcher, unit tested.</summary>
    public static IReadOnlyList<CaptureSession> Match(
        IEnumerable<string> runningProcessNames,
        IEnumerable<string> watchList)
    {
        var watched = watchList
            .Select(NormalizeExeName)
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (watched.Count == 0)
        {
            return [];
        }

        var sessions = new List<CaptureSession>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var running in runningProcessNames)
        {
            var normalized = NormalizeExeName(running);
            if (normalized.Length == 0 || !watched.Contains(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            sessions.Add(new CaptureSession
            {
                RegistryKey = normalized,
                Capability = CaptureCapability.ProcessHeuristic,
                ExecutablePath = null,
                AppKey = normalized,
                AppName = Path.GetFileNameWithoutExtension(normalized),
                IsPackaged = false,
                StartedAtUtc = null,
            });
        }

        return sessions;
    }

    /// <summary>Accepts "obs64", "obs64.exe" or a full path and returns "obs64.exe" lowercased.</summary>
    public static string NormalizeExeName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(trimmed);
        if (fileName.Length == 0)
        {
            fileName = trimmed;
        }

        if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".exe";
        }

        return fileName.ToLowerInvariant();
    }

    private static IReadOnlyList<string> DefaultRunningProcessNames()
    {
        try
        {
            return Process.GetProcesses().Select(p =>
            {
                try
                {
                    return p.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    return string.Empty;
                }
                finally
                {
                    p.Dispose();
                }
            }).Where(n => n.Length > 0).ToList();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            return [];
        }
    }
}
