using HdrSwitch.Core.Config;
using HdrSwitch.Core.Hdr;
using HdrSwitch.Core.Rules;
using Xunit;

namespace HdrSwitch.Tests;

public class AutoHdrSettingsTests
{
    // The exact value observed on a live Windows 11 25H2 machine.
    private const string Live = "AutoHDREnable=1;SwapEffectUpgradeEnable=1;VRROptimizeEnable=1;";

    [Fact]
    public void ReadToken_FindsTheAutoHdrFlag()
    {
        Assert.Equal("1", AutoHdrSettings.ReadToken(Live, "AutoHDREnable"));
    }

    [Fact]
    public void ReadToken_IsCaseInsensitive()
    {
        Assert.Equal("1", AutoHdrSettings.ReadToken(Live, "autohdrenable"));
    }

    [Fact]
    public void ReadToken_ReturnsNullWhenAbsent()
    {
        Assert.Null(AutoHdrSettings.ReadToken(Live, "SomethingElse"));
    }

    [Fact]
    public void WriteToken_PreservesSiblingSettings()
    {
        // Clobbering SwapEffectUpgradeEnable or VRROptimizeEnable would silently change unrelated
        // DirectX behaviour, which is why the value is rewritten token-by-token.
        var updated = AutoHdrSettings.WriteToken(Live, "AutoHDREnable", "0");

        Assert.Equal("AutoHDREnable=0;SwapEffectUpgradeEnable=1;VRROptimizeEnable=1;", updated);
    }

    [Fact]
    public void WriteToken_AppendsWhenTokenIsMissing()
    {
        var updated = AutoHdrSettings.WriteToken("VRROptimizeEnable=1;", "AutoHDREnable", "1");

        Assert.Equal("VRROptimizeEnable=1;AutoHDREnable=1;", updated);
    }

    [Fact]
    public void WriteToken_OnEmptyValue_CreatesTheToken()
    {
        Assert.Equal("AutoHDREnable=1;", AutoHdrSettings.WriteToken(string.Empty, "AutoHDREnable", "1"));
    }

    [Fact]
    public void WriteToken_RoundTripsThroughReadToken()
    {
        var updated = AutoHdrSettings.WriteToken(Live, "AutoHDREnable", "0");
        Assert.Equal("0", AutoHdrSettings.ReadToken(updated, "AutoHDREnable"));
        Assert.Equal("1", AutoHdrSettings.ReadToken(updated, "VRROptimizeEnable"));
    }
}

public class HotkeyParserTests
{
    [Theory]
    [InlineData("Ctrl+Alt+H", "Ctrl+Alt+H")]
    [InlineData("ctrl+alt+h", "Ctrl+Alt+H")]
    [InlineData("Win+Shift+F9", "Shift+Win+F9")]
    [InlineData("  Ctrl + Alt + Delete  ", "Ctrl+Alt+Delete")]
    [InlineData("Alt+Space", "Alt+Space")]
    [InlineData("Ctrl+Shift+5", "Ctrl+Shift+5")]
    public void TryParse_AcceptsCommonCombinations(string input, string expectedText)
    {
        Assert.True(HotkeyParser.TryParse(input, out var hotkey, out var error));
        Assert.Null(error);
        Assert.Equal(expectedText, hotkey!.Text);
    }

    [Fact]
    public void TryParse_NormalisesTheDisplayText()
    {
        Assert.True(HotkeyParser.TryParse("alt+ctrl+h", out var hotkey, out _));
        Assert.Equal("Ctrl+Alt+H", hotkey!.Text);
    }

    [Fact]
    public void TryParse_MapsFunctionKeys()
    {
        Assert.True(HotkeyParser.TryParse("Win+Shift+F9", out var hotkey, out _));
        Assert.Equal((uint)(0x70 + 8), hotkey!.VirtualKey);
        Assert.Equal(HotkeyDefinition.MOD_WIN | HotkeyDefinition.MOD_SHIFT, hotkey.Modifiers);
    }

    [Fact]
    public void TryParse_RejectsAKeyWithoutModifiers()
    {
        // Registering a bare key globally would swallow it system-wide.
        Assert.False(HotkeyParser.TryParse("H", out _, out var error));
        Assert.Contains("modifier", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_RejectsModifiersWithoutAKey()
    {
        Assert.False(HotkeyParser.TryParse("Ctrl+Alt", out _, out var error));
        Assert.Contains("no main key", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryParse_RejectsTwoMainKeys()
    {
        Assert.False(HotkeyParser.TryParse("Ctrl+H+J", out _, out _));
    }

    [Fact]
    public void TryParse_RejectsUnknownKeyNames()
    {
        Assert.False(HotkeyParser.TryParse("Ctrl+Banana", out _, out var error));
        Assert.Contains("Banana", error!);
    }

    [Fact]
    public void TryParse_RejectsEmptyInput()
    {
        Assert.False(HotkeyParser.TryParse("  ", out _, out _));
    }

    [Fact]
    public void ModifiersForRegistration_AddsNoRepeat()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+H", out var hotkey, out _));
        Assert.Equal(HotkeyDefinition.MOD_NOREPEAT, hotkey!.ModifiersForRegistration & HotkeyDefinition.MOD_NOREPEAT);
    }
}

public class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "HdrSwitchTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();

        Assert.True(settings.WatchScreenSharing);
        Assert.Equal("Ctrl+Alt+H", settings.Hotkey);
        Assert.Empty(settings.AppRules);
        Assert.Null(store.LoadWarning);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsThroughTheSourceGeneratedContext()
    {
        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();

        settings.Hotkey = "Win+Shift+F9";
        settings.ToastSeconds = 42;
        settings.WatchGames = true;
        settings.AppRules.Add(new AppRule
        {
            AppKey = "discord.exe",
            DisplayName = "Discord",
            State = RuleState.AutoTurnOff,
            TurnOffCount = 2,
        });
        settings.GameRules.Add(new GameRule { ExeName = "game.exe", DisplayName = "Game", Enabled = true });

        store.Save(settings);
        var reloaded = new SettingsStore(SettingsPath).Load();

        Assert.Equal("Win+Shift+F9", reloaded.Hotkey);
        Assert.Equal(42, reloaded.ToastSeconds);
        Assert.True(reloaded.WatchGames);

        var rule = Assert.Single(reloaded.AppRules);
        Assert.Equal("discord.exe", rule.AppKey);
        Assert.Equal(RuleState.AutoTurnOff, rule.State);
        Assert.Equal(2, rule.TurnOffCount);

        Assert.Equal("game.exe", Assert.Single(reloaded.GameRules).ExeName);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_FallsBackToDefaultsAndPreservesTheFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ this is not json");

        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();

        Assert.NotNull(store.LoadWarning);
        Assert.True(settings.WatchScreenSharing);
        Assert.True(File.Exists(SettingsPath + ".corrupt"),
            "the unreadable file should be kept for inspection rather than discarded");
    }

    [Fact]
    public void Clone_IsDeep()
    {
        var settings = new AppSettings();
        settings.AppRules.Add(new AppRule { AppKey = "a.exe", State = RuleState.AutoKeep });

        var clone = settings.Clone();
        clone.AppRules[0].State = RuleState.Ask;
        clone.Hotkey = "Ctrl+Alt+J";

        Assert.Equal(RuleState.AutoKeep, settings.AppRules[0].State);
        Assert.Equal("Ctrl+Alt+H", settings.Hotkey);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }
}

public class DisplaySelectionTests
{
    private static DisplayTarget Display(int index, string name, HdrCapability capability = HdrCapability.Supported) =>
        new()
        {
            Index = index,
            FriendlyName = name,
            DevicePath = $@"\\?\DISPLAY#TEST{index}#",
            AdapterId = 0x10000 + index,
            TargetId = (uint)(4352 + index),
            Capability = capability,
            HdrEnabled = false,
        };

    private static readonly IReadOnlyList<DisplayTarget> Displays =
    [
        Display(0, "TL156VDXP0101", HdrCapability.Unsupported),
        Display(1, "LS27AG55x"),
    ];

    [Fact]
    public void NullSelector_MatchesEveryCapableDisplay()
    {
        Assert.True(DisplaySelection.TryResolve(Displays, null, out var matched, out _));
        Assert.Equal("LS27AG55x", Assert.Single(matched).Label);
    }

    [Fact]
    public void IndexSelector_IsOneBased()
    {
        Assert.True(DisplaySelection.TryResolve(Displays, "2", out var matched, out _));
        Assert.Equal("LS27AG55x", Assert.Single(matched).Label);
    }

    [Fact]
    public void IndexSelector_CanTargetAnUnsupportedDisplay()
    {
        // Explicitly naming a display should resolve it; whether it can toggle is decided later,
        // so the caller can explain why instead of saying "no match".
        Assert.True(DisplaySelection.TryResolve(Displays, "1", out var matched, out _));
        Assert.Equal(HdrCapability.Unsupported, Assert.Single(matched).Capability);
    }

    [Fact]
    public void NameSelector_MatchesCaseInsensitiveFragment()
    {
        Assert.True(DisplaySelection.TryResolve(Displays, "ls27", out var matched, out _));
        Assert.Equal("LS27AG55x", Assert.Single(matched).Label);
    }

    [Fact]
    public void OutOfRangeIndex_ReportsTheValidRange()
    {
        Assert.False(DisplaySelection.TryResolve(Displays, "9", out _, out var error));
        Assert.Contains("1..2", error!);
    }

    [Fact]
    public void UnknownName_ListsWhatIsAvailable()
    {
        Assert.False(DisplaySelection.TryResolve(Displays, "nosuch", out _, out var error));
        Assert.Contains("LS27AG55x", error!);
    }

    [Fact]
    public void StableId_PrefersDevicePathOverLuid()
    {
        // LUIDs change across reboots; persisted rules must not key on them.
        var display = Display(1, "LS27AG55x");
        Assert.Equal(display.DevicePath, display.StableId);
    }

    [Fact]
    public void StableId_FallsBackWhenDevicePathIsMissing()
    {
        var display = new DisplayTarget
        {
            Index = 0,
            FriendlyName = "Some Panel",
            DevicePath = string.Empty,
            AdapterId = 1,
            TargetId = 2,
            Capability = HdrCapability.Supported,
            HdrEnabled = false,
        };

        Assert.Equal("name:Some Panel", display.StableId);
    }
}
