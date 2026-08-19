using HdrSwitch.Core.Sharing;
using Xunit;

namespace HdrSwitch.Tests;

/// <summary>In-memory stand-in for HKCU, shaped like the real consent store.</summary>
internal sealed class FakeRegistryProbe : IRegistryProbe
{
    private readonly Dictionary<string, List<string>> _subKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _values = new(StringComparer.OrdinalIgnoreCase);

    internal FakeRegistryProbe AddApp(string capability, string appKey, long start, long stop, bool packaged = false)
    {
        var parent = packaged
            ? $@"{ConsentStoreReader.ConsentStoreRoot}\{capability}"
            : $@"{ConsentStoreReader.ConsentStoreRoot}\{capability}\NonPackaged";

        if (!_subKeys.TryGetValue(parent, out var list))
        {
            list = [];
            _subKeys[parent] = list;
        }

        list.Add(appKey);
        _values[$@"{parent}\{appKey}|LastUsedTimeStart"] = start;
        _values[$@"{parent}\{appKey}|LastUsedTimeStop"] = stop;
        return this;
    }

    public IReadOnlyList<string> GetSubKeyNames(string hkcuPath) =>
        _subKeys.TryGetValue(hkcuPath, out var list) ? list : [];

    public long? GetQwordValue(string hkcuPath, string valueName) =>
        _values.TryGetValue($"{hkcuPath}|{valueName}", out var value) ? value : null;
}

public class ConsentStoreReaderTests
{
    private const long Start = 134315371456519307;
    private const long Stop = 134315388496868601;

    [Theory]
    // In progress: Windows zeroes the stop time while capture is live.
    [InlineData(Start, 0L, true)]
    [InlineData(Start, null, true)]
    // A stop older than the start means a new capture began before the old record closed.
    [InlineData(Stop, Start, true)]
    // Finished.
    [InlineData(Start, Stop, false)]
    // Never started.
    [InlineData(0L, 0L, false)]
    [InlineData(null, 0L, false)]
    public void IsActive_MatchesTheConsentStoreConvention(long? start, long? stop, bool expected)
    {
        Assert.Equal(expected, ConsentStoreReader.IsActive(start, stop));
    }

    [Fact]
    public void DecodeExecutablePath_ReplacesHashWithPathSeparator()
    {
        const string key = @"C:#Users#cagat#AppData#Local#Discord#app-1.0.9254#Discord.exe";

        Assert.Equal(
            @"C:\Users\cagat\AppData\Local\Discord\app-1.0.9254\Discord.exe",
            ConsentStoreReader.DecodeExecutablePath(key));
    }

    [Fact]
    public void DeriveAppKey_IsStableAcrossAppUpdates()
    {
        // Discord reinstalls into a version-stamped folder on every update. Keying rules on the
        // full path would silently discard what the user taught us after each patch.
        var older = ConsentStoreReader.DeriveAppKey(
            @"C:#Users#cagat#AppData#Local#Discord#app-1.0.9238#Discord.exe", isPackaged: false);
        var newer = ConsentStoreReader.DeriveAppKey(
            @"C:#Users#cagat#AppData#Local#Discord#app-1.0.9254#Discord.exe", isPackaged: false);

        Assert.Equal("discord.exe", older);
        Assert.Equal(older, newer);
    }

    [Fact]
    public void DeriveAppKey_ForPackagedApp_UsesTheFamilyName()
    {
        Assert.Equal(
            "microsoft.skypeapp_kzf8qxf38zg5c",
            ConsentStoreReader.DeriveAppKey("Microsoft.SkypeApp_kzf8qxf38zg5c", isPackaged: true));
    }

    [Fact]
    public void DeriveAppName_PrefersProductNameWhenAvailable()
    {
        var name = ConsentStoreReader.DeriveAppName(
            @"C:#Program Files#Google#Chrome#Application#chrome.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            isPackaged: false,
            _ => "Google Chrome");

        Assert.Equal("Google Chrome", name);
    }

    [Fact]
    public void DeriveAppName_FallsBackToFileNameWhenProductLookupFails()
    {
        var name = ConsentStoreReader.DeriveAppName(
            @"C:#Program Files#obs-studio#bin#64bit#obs64.exe",
            @"C:\Program Files\obs-studio\bin\64bit\obs64.exe",
            isPackaged: false,
            _ => null);

        Assert.Equal("obs64", name);
    }

    [Fact]
    public void DeriveAppName_ForPackagedApp_StripsPublisherAndHash()
    {
        Assert.Equal("SkypeApp",
            ConsentStoreReader.DeriveAppName("Microsoft.SkypeApp_kzf8qxf38zg5c", null, isPackaged: true));
    }

    [Fact]
    public void GetActiveSessions_ReturnsOnlyLiveCaptures()
    {
        var probe = new FakeRegistryProbe()
            .AddApp(ConsentStoreReader.ProgrammaticCapability,
                @"C:#Users#me#AppData#Local#Discord#app-1.0.9254#Discord.exe", Start, 0)
            .AddApp(ConsentStoreReader.ProgrammaticCapability,
                @"C:#Program Files#obs-studio#bin#64bit#obs64.exe", Start, Stop);

        var sessions = ConsentStoreReader.GetActiveSessions(probe);

        var session = Assert.Single(sessions);
        Assert.Equal("discord.exe", session.AppKey);
        Assert.Equal(CaptureCapability.Programmatic, session.Capability);
        Assert.False(session.IsPackaged);
    }

    [Fact]
    public void GetActiveSessions_DeduplicatesAcrossBothCapabilities()
    {
        // Discord registers under both graphicsCaptureProgrammatic and
        // graphicsCaptureWithoutBorder for a single share; the user must be prompted once.
        const string key = @"C:#Users#me#AppData#Local#Discord#app-1.0.9254#Discord.exe";

        var probe = new FakeRegistryProbe()
            .AddApp(ConsentStoreReader.ProgrammaticCapability, key, Start, 0)
            .AddApp(ConsentStoreReader.WithoutBorderCapability, key, Start, 0);

        Assert.Single(ConsentStoreReader.GetActiveSessions(probe));
    }

    [Fact]
    public void GetActiveSessions_HandlesPackagedApps()
    {
        var probe = new FakeRegistryProbe()
            .AddApp(ConsentStoreReader.WithoutBorderCapability,
                "Microsoft.SkypeApp_kzf8qxf38zg5c", Start, 0, packaged: true);

        var session = Assert.Single(ConsentStoreReader.GetActiveSessions(probe));
        Assert.True(session.IsPackaged);
        Assert.Null(session.ExecutablePath);
        Assert.Equal("SkypeApp", session.AppName);
    }

    [Fact]
    public void GetActiveSessions_IgnoresTheNonPackagedContainerKey()
    {
        // "NonPackaged" is a folder, not an app, and must never be reported as one.
        var probe = new FakeRegistryProbe()
            .AddApp(ConsentStoreReader.ProgrammaticCapability, "NonPackaged", Start, 0, packaged: true);

        Assert.Empty(ConsentStoreReader.GetActiveSessions(probe));
    }

    [Fact]
    public void GetActiveSessions_OnEmptyRegistry_ReturnsNothing()
    {
        Assert.Empty(ConsentStoreReader.GetActiveSessions(new FakeRegistryProbe()));
    }
}
