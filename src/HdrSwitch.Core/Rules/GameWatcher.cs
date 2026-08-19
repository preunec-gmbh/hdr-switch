using System.Diagnostics;
using HdrSwitch.Core.Config;
using HdrSwitch.Core.Sharing;

namespace HdrSwitch.Core.Rules;

/// <summary>
/// The inverse of the sharing rule: turn HDR on while a chosen game is running, and put it back
/// when the game exits.
///
/// This polls process names rather than subscribing to Win32_ProcessStartTrace, because that WMI
/// event class requires elevation and HDR Switch is deliberately asInvoker. A three-second poll
/// over process names is cheap and is fast enough for a game launch.
/// </summary>
public sealed class GameWatcher : IDisposable
{
    private const int DefaultPollMs = 3000;

    private readonly Func<IReadOnlyList<GameRule>> _rulesProvider;
    private readonly Func<IReadOnlyList<string>> _runningProcessNames;
    private readonly int _pollMs;
    private readonly ManualResetEvent _stopEvent = new(false);
    private readonly object _stateLock = new();

    private HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);
    private Thread? _thread;
    private bool _disposed;

    public GameWatcher(
        Func<IReadOnlyList<GameRule>> rulesProvider,
        Func<IReadOnlyList<string>>? runningProcessNames = null,
        int pollMs = DefaultPollMs)
    {
        _rulesProvider = rulesProvider;
        _runningProcessNames = runningProcessNames ?? DefaultRunningProcessNames;
        _pollMs = pollMs;
    }

    /// <summary>Raised on a background thread when a watched game appears.</summary>
    public event EventHandler<GameRule>? GameStarted;

    /// <summary>Raised on a background thread when a watched game exits.</summary>
    public event EventHandler<GameRule>? GameStopped;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread is not null)
        {
            return;
        }

        // Seed silently: a game already running at startup is not a transition.
        lock (_stateLock)
        {
            _running = CurrentMatches();
        }

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = "HdrSwitch.GameWatcher",
        };
        _thread.Start();
    }

    public void Poll()
    {
        var rules = _rulesProvider().Where(r => r.Enabled).ToList();
        var current = CurrentMatches();

        List<string> started;
        List<string> stopped;

        lock (_stateLock)
        {
            started = current.Except(_running, StringComparer.OrdinalIgnoreCase).ToList();
            stopped = _running.Except(current, StringComparer.OrdinalIgnoreCase).ToList();
            _running = current;
        }

        foreach (var exe in stopped)
        {
            var rule = FindRule(rules, exe);
            if (rule is not null)
            {
                GameStopped?.Invoke(this, rule);
            }
        }

        foreach (var exe in started)
        {
            var rule = FindRule(rules, exe);
            if (rule is not null)
            {
                GameStarted?.Invoke(this, rule);
            }
        }
    }

    private static GameRule? FindRule(IEnumerable<GameRule> rules, string exeName) =>
        rules.FirstOrDefault(r =>
            string.Equals(ProcessHeuristic.NormalizeExeName(r.ExeName), exeName, StringComparison.OrdinalIgnoreCase));

    private HashSet<string> CurrentMatches()
    {
        var rules = _rulesProvider().Where(r => r.Enabled).ToList();
        if (rules.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return MatchRunning(_runningProcessNames(), rules);
    }

    /// <summary>Pure matcher, unit tested.</summary>
    public static HashSet<string> MatchRunning(IEnumerable<string> runningProcessNames, IEnumerable<GameRule> rules)
    {
        var wanted = rules
            .Where(r => r.Enabled)
            .Select(r => ProcessHeuristic.NormalizeExeName(r.ExeName))
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
        {
            return matches;
        }

        foreach (var running in runningProcessNames)
        {
            var normalized = ProcessHeuristic.NormalizeExeName(running);
            if (wanted.Contains(normalized))
            {
                matches.Add(normalized);
            }
        }

        return matches;
    }

    private void Loop()
    {
        while (!_stopEvent.WaitOne(_pollMs))
        {
            try
            {
                Poll();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Debug.WriteLine($"[GameWatcher] poll failed: {ex}");
            }
        }
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopEvent.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _stopEvent.Dispose();
    }
}
