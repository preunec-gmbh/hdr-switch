using System.Collections.Concurrent;
using System.Diagnostics;
using HdrSwitch.Core.Interop;
using Microsoft.Win32;

namespace HdrSwitch.Core.Sharing;

/// <summary>
/// Watches the CapabilityAccessManager consent store and raises events when an application
/// starts or stops capturing the screen.
///
/// Detection is event-driven via RegNotifyChangeKeyValue, so a capture is normally noticed
/// within a few hundred milliseconds. A slow safety poll also runs, because a missed
/// notification would otherwise mean the feature quietly stops working until restart.
///
/// Events are raised on a background thread; UI callers must marshal.
/// </summary>
public sealed class CaptureWatcher : IDisposable
{
    private const int DebounceMs = 300;
    private const int SafetyPollMs = 10_000;

    private readonly IRegistryProbe _probe;
    private readonly Func<IReadOnlyList<CaptureSession>>? _heuristicProvider;
    private readonly ConcurrentDictionary<string, string> _productNameCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly ManualResetEvent _stopEvent = new(false);
    private readonly object _stateLock = new();

    private Dictionary<string, CaptureSession> _active = new(StringComparer.OrdinalIgnoreCase);
    private Thread? _thread;
    private bool _disposed;

    public CaptureWatcher(IRegistryProbe? probe = null, Func<IReadOnlyList<CaptureSession>>? heuristicProvider = null)
    {
        _probe = probe ?? new RegistryProbe();
        _heuristicProvider = heuristicProvider;
    }

    /// <summary>Raised when an app begins capturing. Background thread.</summary>
    public event EventHandler<CaptureSession>? CaptureStarted;

    /// <summary>Raised when an app stops capturing. Background thread.</summary>
    public event EventHandler<CaptureSession>? CaptureStopped;

    /// <summary>Raised when the watcher cannot arm registry notifications and is poll-only.</summary>
    public event EventHandler<string>? Degraded;

    public IReadOnlyList<CaptureSession> ActiveSessions
    {
        get
        {
            lock (_stateLock)
            {
                return _active.Values.ToList();
            }
        }
    }

    public bool IsCapturing => ActiveSessions.Count > 0;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_thread is not null)
        {
            return;
        }

        // Seed state without raising events: a capture already running when HDR Switch starts
        // is not a transition the user needs to be told about.
        lock (_stateLock)
        {
            _active = Scan().ToDictionary(s => s.AppKey, StringComparer.OrdinalIgnoreCase);
        }

        _thread = new Thread(WatchLoop)
        {
            IsBackground = true,
            Name = "HdrSwitch.CaptureWatcher",
        };
        _thread.Start();
    }

    /// <summary>Forces an immediate rescan and diff. Used by the safety poll and by tests.</summary>
    public void Poll() => DiffAndRaise(Scan());

    private void WatchLoop()
    {
        var keys = new List<RegistryKey?>();
        var events = new List<AutoResetEvent>();
        var armed = new List<bool>();

        foreach (var (capabilityName, _) in ConsentStoreReader.Capabilities)
        {
            keys.Add(null);
            events.Add(new AutoResetEvent(false));
            armed.Add(false);
            _ = capabilityName;
        }

        var reportedDegraded = false;

        try
        {
            while (true)
            {
                // (Re)open and (re)arm any capability key that is not currently watched. Keys can
                // appear later -- graphicsCaptureWithoutBorder may not exist until first use.
                var anyArmed = false;
                for (var i = 0; i < ConsentStoreReader.Capabilities.Count; i++)
                {
                    if (armed[i])
                    {
                        anyArmed = true;
                        continue;
                    }

                    keys[i]?.Dispose();
                    keys[i] = OpenCapabilityKey(ConsentStoreReader.Capabilities[i].Name);

                    if (keys[i] is not null && TryArm(keys[i]!, events[i]))
                    {
                        armed[i] = true;
                        anyArmed = true;
                    }
                }

                if (!anyArmed && !reportedDegraded)
                {
                    reportedDegraded = true;
                    Degraded?.Invoke(this,
                        "Could not subscribe to registry change notifications; " +
                        "falling back to polling every 10 seconds.");
                }

                var handles = new WaitHandle[events.Count + 1];
                handles[0] = _stopEvent;
                for (var i = 0; i < events.Count; i++)
                {
                    handles[i + 1] = events[i];
                }

                var signalled = WaitHandle.WaitAny(handles, SafetyPollMs);
                if (signalled == 0)
                {
                    return;
                }

                if (signalled != WaitHandle.WaitTimeout)
                {
                    // RegNotifyChangeKeyValue is one-shot; mark for re-arming next iteration.
                    armed[signalled - 1] = false;

                    // Windows writes Start and Stop as separate operations. Debouncing avoids
                    // reacting to a half-written record.
                    if (_stopEvent.WaitOne(DebounceMs))
                    {
                        return;
                    }
                }

                try
                {
                    DiffAndRaise(Scan());
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Debug.WriteLine($"[CaptureWatcher] scan failed: {ex}");
                }
            }
        }
        finally
        {
            foreach (var key in keys)
            {
                key?.Dispose();
            }

            foreach (var handle in events)
            {
                handle.Dispose();
            }
        }
    }

    private static RegistryKey? OpenCapabilityKey(string capabilityName)
    {
        try
        {
            return Registry.CurrentUser.OpenSubKey($@"{ConsentStoreReader.ConsentStoreRoot}\{capabilityName}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static bool TryArm(RegistryKey key, AutoResetEvent signal)
    {
        try
        {
            var status = RegistryNative.RegNotifyChangeKeyValue(
                key.Handle,
                watchSubtree: true,
                RegistryNative.REG_NOTIFY_CHANGE_NAME |
                RegistryNative.REG_NOTIFY_CHANGE_LAST_SET |
                RegistryNative.REG_NOTIFY_THREAD_AGNOSTIC,
                signal.SafeWaitHandle,
                asynchronous: true);

            return status == 0;
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException)
        {
            return false;
        }
    }

    private IReadOnlyList<CaptureSession> Scan()
    {
        var sessions = ConsentStoreReader
            .GetActiveSessions(_probe, LookupProductName)
            .ToList();

        if (_heuristicProvider is not null)
        {
            var known = sessions.Select(s => s.AppKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var extra in _heuristicProvider())
            {
                if (known.Add(extra.AppKey))
                {
                    sessions.Add(extra);
                }
            }
        }

        return sessions;
    }

    private void DiffAndRaise(IReadOnlyList<CaptureSession> current)
    {
        List<CaptureSession> started;
        List<CaptureSession> stopped;

        lock (_stateLock)
        {
            var next = current.ToDictionary(s => s.AppKey, StringComparer.OrdinalIgnoreCase);
            started = next.Where(kv => !_active.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
            stopped = _active.Where(kv => !next.ContainsKey(kv.Key)).Select(kv => kv.Value).ToList();
            _active = next;
        }

        foreach (var session in stopped)
        {
            CaptureStopped?.Invoke(this, session);
        }

        foreach (var session in started)
        {
            CaptureStarted?.Invoke(this, session);
        }
    }

    private string? LookupProductName(string executablePath)
    {
        if (_productNameCache.TryGetValue(executablePath, out var cached))
        {
            return cached.Length == 0 ? null : cached;
        }

        string resolved = string.Empty;
        try
        {
            if (File.Exists(executablePath))
            {
                var info = FileVersionInfo.GetVersionInfo(executablePath);
                resolved = FirstNonEmpty(info.ProductName, info.FileDescription) ?? string.Empty;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            resolved = string.Empty;
        }

        _productNameCache[executablePath] = resolved;
        return resolved.Length == 0 ? null : resolved;
    }

    private static string? FirstNonEmpty(params string?[] candidates) =>
        candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

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
