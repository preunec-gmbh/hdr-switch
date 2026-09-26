namespace HdrSwitch.Core.Sharing;

/// <summary>
/// Turns the raw consent-store signal ("this app has a capture open") into "this app is actually
/// sharing the screen".
///
/// The raw signal is too eager. Measured against Chrome 153 on Windows 11, a Google Meet share
/// looks like this in the consent store:
///
///   picker opens         Start written, Stop = 0     (live thumbnails of every screen/window)
///   user presses Share   Stop written                (thumbnails torn down)
///   ~3 s later           a NEW Start, Stop = 0       (the real capture)
///   user cancels         Stop written, nothing after
///
/// So in a Chromium browser the first capture is the source picker, not a share. Acting on it
/// switched HDR off while the user was still choosing -- and then, on Share, the Stop restored it
/// and the real Start switched it off again.
///
/// The rules, per app:
/// <list type="bullet">
///   <item>Every new capture is <em>pending</em>. A capture that ends while pending was never a
///   share (a cancelled picker, or a single-frame thumbnail grab) and raises nothing.</item>
///   <item>An ordinary app is confirmed once its capture has stayed open for <see cref="SettleTime"/>.</item>
///   <item>A <see cref="IsPickerApp">picker app</see> is confirmed only by the hand-off: a capture
///   that begins within <see cref="HandoffWindow"/> of the previous one ending. A capture that
///   simply stays open is the picker sitting on screen, and is confirmed only after
///   <see cref="PickerTimeout"/>, as a backstop for shares that skip the picker.</item>
///   <item>A confirmed capture that ends is held for <see cref="StopGrace"/> before it counts as
///   stopped, so switching the shared source does not flap HDR off and on.</item>
/// </list>
///
/// Pure and clock-driven, so it can be tested without a screen share.
/// </summary>
public sealed class CaptureConfirmer
{
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1.5);
    public static readonly TimeSpan HandoffSettleTime = TimeSpan.FromSeconds(0.75);
    public static readonly TimeSpan HandoffWindow = TimeSpan.FromSeconds(8);
    public static readonly TimeSpan PickerTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan StopGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Chromium browsers: getDisplayMedia shows a picker that captures live thumbnails, which
    /// Windows records exactly like a share.
    /// </summary>
    public static IReadOnlySet<string> PickerApps { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe",
        "msedge.exe",
        "brave.exe",
        "opera.exe",
        "vivaldi.exe",
        "chromium.exe",
        "arc.exe",
        "thorium.exe",
    };

    private enum Phase
    {
        Pending,
        Confirmed,
        Stopping,
    }

    private sealed class Entry
    {
        public required CaptureSession Session { get; set; }
        public required Phase Phase { get; set; }
        public required DateTime Since { get; set; }
        public bool IsHandoff { get; init; }
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When each app's most recent capture ended, confirmed or not.</summary>
    private readonly Dictionary<string, DateTime> _lastEnded = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsPickerApp(CaptureSession session) =>
        session.Capability != CaptureCapability.ProcessHeuristic && PickerApps.Contains(session.AppKey);

    /// <summary>Sessions currently counted as sharing, including ones inside their stop grace.</summary>
    public IReadOnlyList<CaptureSession> Confirmed =>
        _entries.Values.Where(e => e.Phase != Phase.Pending).Select(e => e.Session).ToList();

    /// <summary>
    /// Adopt captures that were already running at startup as confirmed, without raising
    /// anything: they are not a transition the user needs telling about.
    /// </summary>
    public void Seed(IEnumerable<CaptureSession> active, DateTime now)
    {
        foreach (var session in active)
        {
            _entries[session.AppKey] = new Entry { Session = session, Phase = Phase.Confirmed, Since = now };
        }
    }

    /// <summary>
    /// Feed the latest raw scan. Returns the sessions that became confirmed and the ones whose
    /// stop is now final.
    /// </summary>
    public (IReadOnlyList<CaptureSession> Started, IReadOnlyList<CaptureSession> Stopped) Update(
        IReadOnlyList<CaptureSession> rawActive, DateTime now)
    {
        var started = new List<CaptureSession>();
        var stopped = new List<CaptureSession>();
        var active = rawActive.ToDictionary(s => s.AppKey, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, session) in active)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                var handoff = _lastEnded.TryGetValue(key, out var ended) && now - ended <= HandoffWindow;
                _entries[key] = new Entry { Session = session, Phase = Phase.Pending, Since = now, IsHandoff = handoff };
                continue;
            }

            if (entry.Phase == Phase.Pending && !entry.IsHandoff && IsRestart(entry.Session, session))
            {
                // A new Start stamp on a capture that never showed a Stop: the real capture
                // opened before the picker's was closed. Same meaning as the hand-off.
                _entries[key] = new Entry { Session = session, Phase = Phase.Pending, Since = now, IsHandoff = true };
                continue;
            }

            entry.Session = session;
            if (entry.Phase == Phase.Stopping)
            {
                // Came back inside the grace period: the same share, carried on.
                entry.Phase = Phase.Confirmed;
            }
        }

        foreach (var (key, entry) in _entries.ToList())
        {
            if (active.ContainsKey(key))
            {
                if (entry.Phase == Phase.Pending && IsReady(entry, now))
                {
                    entry.Phase = Phase.Confirmed;
                    started.Add(entry.Session);
                }

                continue;
            }

            switch (entry.Phase)
            {
                case Phase.Pending:
                    _entries.Remove(key);
                    _lastEnded[key] = now;
                    break;

                case Phase.Confirmed:
                    entry.Phase = Phase.Stopping;
                    entry.Since = now;
                    _lastEnded[key] = now;
                    break;

                case Phase.Stopping when now - entry.Since >= StopGrace:
                    _entries.Remove(key);
                    stopped.Add(entry.Session);
                    break;
            }
        }

        return (started, stopped);
    }

    /// <summary>
    /// The earliest moment an <see cref="Update"/> could change something without a new registry
    /// event, so the watcher knows how long it may sleep. Null when nothing is waiting.
    /// </summary>
    public DateTime? NextDeadline()
    {
        DateTime? next = null;

        foreach (var entry in _entries.Values)
        {
            DateTime? due = entry.Phase switch
            {
                Phase.Pending => entry.Since + RequiredHold(entry),
                Phase.Stopping => entry.Since + StopGrace,
                _ => null,
            };

            if (due is not null && (next is null || due < next))
            {
                next = due;
            }
        }

        return next;
    }

    private static bool IsRestart(CaptureSession previous, CaptureSession current) =>
        previous.StartedAtUtc is { } before && current.StartedAtUtc is { } after && after > before;

    private static bool IsReady(Entry entry, DateTime now) => now - entry.Since >= RequiredHold(entry);

    private static TimeSpan RequiredHold(Entry entry)
    {
        if (!IsPickerApp(entry.Session))
        {
            return SettleTime;
        }

        return entry.IsHandoff ? HandoffSettleTime : PickerTimeout;
    }
}
