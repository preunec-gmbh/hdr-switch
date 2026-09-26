using HdrSwitch.Core.Sharing;
using Xunit;

namespace HdrSwitch.Tests;

/// <summary>
/// The timelines here are the consent-store sequences measured against Chrome 153 on
/// Windows 11 (2026-09-26): picker opens -> Start; Share -> Stop, then a new Start ~3.1 s later;
/// Cancel -> Stop and nothing after.
/// </summary>
public class CaptureConfirmerTests
{
    private static readonly DateTime T0 = new(2026, 9, 26, 4, 57, 50, DateTimeKind.Utc);

    private static CaptureSession Session(string appKey, DateTime startedAt) => new()
    {
        RegistryKey = appKey,
        Capability = CaptureCapability.WithoutBorder,
        AppKey = appKey,
        AppName = appKey,
        IsPackaged = false,
        StartedAtUtc = startedAt,
    };

    private static DateTime At(double seconds) => T0.AddSeconds(seconds);

    [Fact]
    public void ChromePicker_OpenAndCancelled_NeverCountsAsSharing()
    {
        var confirmer = new CaptureConfirmer();
        var picker = Session("chrome.exe", At(0));

        // The user looks at the picker for a while -- well past any ordinary settle time.
        for (var s = 0.0; s <= 20; s += 0.5)
        {
            Assert.Empty(confirmer.Update([picker], At(s)).Started);
        }

        var (started, stopped) = confirmer.Update([], At(21));
        Assert.Empty(started);
        Assert.Empty(stopped);
        Assert.Null(confirmer.NextDeadline());
    }

    [Fact]
    public void ChromePicker_ThenShare_ConfirmsOnlyTheRealCapture()
    {
        var confirmer = new CaptureConfirmer();

        Assert.Empty(confirmer.Update([Session("chrome.exe", At(0))], At(0)).Started);
        Assert.Empty(confirmer.Update([Session("chrome.exe", At(0))], At(30)).Started);

        // Share pressed at 70 s: the thumbnail capture stops...
        Assert.Empty(confirmer.Update([], At(70)).Started);

        // ...and the real capture starts 3.1 s later.
        var real = Session("chrome.exe", At(73.1));
        Assert.Empty(confirmer.Update([real], At(73.1)).Started);

        var (started, _) = confirmer.Update([real], At(73.1) + CaptureConfirmer.HandoffSettleTime);
        Assert.Equal(real, Assert.Single(started));
    }

    [Fact]
    public void ChromeRealCaptureOverlappingThePicker_IsStillConfirmed()
    {
        // If Chrome ever opened the real capture before closing the thumbnails, the registry
        // would show no Stop -- only a newer Start on a capture that stayed open.
        var confirmer = new CaptureConfirmer();
        confirmer.Update([Session("chrome.exe", At(0))], At(0));
        confirmer.Update([Session("chrome.exe", At(0))], At(10));

        var real = Session("chrome.exe", At(12));
        Assert.Empty(confirmer.Update([real], At(12)).Started);
        Assert.Single(confirmer.Update([real], At(12) + CaptureConfirmer.HandoffSettleTime).Started);
    }

    [Fact]
    public void ChromeWithoutPicker_IsConfirmedAfterTheBackstop()
    {
        var confirmer = new CaptureConfirmer();
        var capture = Session("chrome.exe", At(0));

        confirmer.Update([capture], At(0));
        Assert.Empty(confirmer.Update([capture], At(59)).Started);
        Assert.Single(confirmer.Update([capture], At(0) + CaptureConfirmer.PickerTimeout).Started);
    }

    [Fact]
    public void OrdinaryApp_IsConfirmedAfterTheSettleTime()
    {
        var confirmer = new CaptureConfirmer();
        var discord = Session("discord.exe", At(0));

        Assert.Empty(confirmer.Update([discord], At(0)).Started);
        Assert.Empty(confirmer.Update([discord], At(1)).Started);
        Assert.Equal(At(0) + CaptureConfirmer.SettleTime, confirmer.NextDeadline());
        Assert.Single(confirmer.Update([discord], At(0) + CaptureConfirmer.SettleTime).Started);
    }

    [Fact]
    public void ThumbnailGrab_OfAFewMilliseconds_IsIgnored()
    {
        // Discord's records show Start/Stop pairs 5 ms apart: single-frame previews.
        var confirmer = new CaptureConfirmer();
        confirmer.Update([Session("discord.exe", At(0))], At(0));

        var (started, stopped) = confirmer.Update([], At(0.3));
        Assert.Empty(started);
        Assert.Empty(stopped);
    }

    [Fact]
    public void ConfirmedShare_StopsOnlyAfterTheGracePeriod()
    {
        var confirmer = new CaptureConfirmer();
        var discord = Session("discord.exe", At(0));
        confirmer.Update([discord], At(0));
        confirmer.Update([discord], At(2));

        Assert.Empty(confirmer.Update([], At(100)).Stopped);
        Assert.Single(confirmer.Confirmed);
        Assert.Empty(confirmer.Update([], At(102)).Stopped);

        var (_, stopped) = confirmer.Update([], At(100) + CaptureConfirmer.StopGrace);
        Assert.Equal(discord, Assert.Single(stopped));
        Assert.Empty(confirmer.Confirmed);
    }

    [Fact]
    public void SwitchingTheSharedSource_DoesNotFlap()
    {
        var confirmer = new CaptureConfirmer();
        confirmer.Update([Session("chrome.exe", At(0))], At(0));
        confirmer.Update([], At(5));
        confirmer.Update([Session("chrome.exe", At(8))], At(8));
        Assert.Single(confirmer.Update([Session("chrome.exe", At(8))], At(9)).Started);

        // Stop, then a new capture 3 s later: inside the grace period, so the same share.
        var (started, stopped) = confirmer.Update([], At(100));
        Assert.Empty(stopped);
        (started, stopped) = confirmer.Update([Session("chrome.exe", At(103))], At(103));
        Assert.Empty(started);
        Assert.Empty(stopped);
        Assert.Empty(confirmer.Update([Session("chrome.exe", At(103))], At(120)).Stopped);
    }

    [Fact]
    public void SeededCaptures_AreConfirmedSilently()
    {
        var confirmer = new CaptureConfirmer();
        var obs = Session("obs64.exe", At(-600));
        confirmer.Seed([obs], At(0));

        Assert.Empty(confirmer.Update([obs], At(1)).Started);
        Assert.Single(confirmer.Confirmed);
    }

    [Fact]
    public void ProcessHeuristic_IsNeverTreatedAsAPicker()
    {
        var running = Session("chrome.exe", At(0)) with { Capability = CaptureCapability.ProcessHeuristic };
        Assert.False(CaptureConfirmer.IsPickerApp(running));

        var confirmer = new CaptureConfirmer();
        confirmer.Update([running], At(0));
        Assert.Single(confirmer.Update([running], At(0) + CaptureConfirmer.SettleTime).Started);
    }

    [Fact]
    public void Watcher_RaisesStartedOnlyOnceTheShareIsReal()
    {
        var now = T0;
        var probe = new MutableProbe();
        using var watcher = new CaptureWatcher(probe, utcNow: () => now);
        var started = new List<CaptureSession>();
        watcher.CaptureStarted += (_, s) => started.Add(s);

        // Driven by Poll() alone: Start() would add the background thread and a race.

        probe.Set("chrome.exe", At(0), stop: null);
        watcher.Poll();
        now = At(20);
        watcher.Poll();
        Assert.Empty(started);

        probe.Set("chrome.exe", At(0), stop: At(20.5));
        now = At(20.5);
        watcher.Poll();

        probe.Set("chrome.exe", At(23.6), stop: null);
        now = At(23.6);
        watcher.Poll();
        now = At(25);
        watcher.Poll();

        Assert.Equal("chrome.exe", Assert.Single(started).AppKey);
    }

    /// <summary>One app under graphicsCaptureWithoutBorder whose timestamps can change.</summary>
    private sealed class MutableProbe : IRegistryProbe
    {
        private const string Parent =
            $@"{ConsentStoreReader.ConsentStoreRoot}\{ConsentStoreReader.WithoutBorderCapability}\NonPackaged";

        private string? _key;
        private long _start;
        private long _stop;

        internal void Set(string exe, DateTime start, DateTime? stop)
        {
            _key = @"C:#Program Files#Google#Chrome#Application#" + exe;
            _start = start.ToFileTimeUtc();
            _stop = stop?.ToFileTimeUtc() ?? 0;
        }

        public IReadOnlyList<string> GetSubKeyNames(string hkcuPath) =>
            _key is not null && string.Equals(hkcuPath, Parent, StringComparison.OrdinalIgnoreCase) ? [_key] : [];

        public long? GetQwordValue(string hkcuPath, string valueName) => valueName switch
        {
            "LastUsedTimeStart" => _start,
            "LastUsedTimeStop" => _stop,
            _ => null,
        };
    }
}
