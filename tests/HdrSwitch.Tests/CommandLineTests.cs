using HdrSwitch.Core.Cli;
using HdrSwitch.Core.Config;
using HdrSwitch.Core.Rules;
using HdrSwitch.Core.Sharing;
using Xunit;

namespace HdrSwitch.Tests;

public class CommandLineTests
{
    [Fact]
    public void NoArguments_RunsTheTray()
    {
        var options = CommandLine.Parse([]);

        Assert.Equal(CliCommand.Tray, options.Command);
        Assert.False(options.IsConsoleCommand);
        Assert.Null(options.Error);
    }

    [Theory]
    [InlineData("on", CliCommand.On)]
    [InlineData("enable", CliCommand.On)]
    [InlineData("off", CliCommand.Off)]
    [InlineData("disable", CliCommand.Off)]
    [InlineData("toggle", CliCommand.Toggle)]
    [InlineData("status", CliCommand.Status)]
    [InlineData("list", CliCommand.List)]
    [InlineData("selftest", CliCommand.SelfTest)]
    public void Commands_AreRecognised(string arg, CliCommand expected)
    {
        var options = CommandLine.Parse([arg]);

        Assert.Equal(expected, options.Command);
        Assert.True(options.IsConsoleCommand);
        Assert.Null(options.Error);
    }

    [Fact]
    public void CommandsAreCaseInsensitive()
    {
        Assert.Equal(CliCommand.Toggle, CommandLine.Parse(["TOGGLE"]).Command);
    }

    [Fact]
    public void DisplayOption_AcceptsSeparateValue()
    {
        var options = CommandLine.Parse(["on", "--display", "2"]);

        Assert.Equal(CliCommand.On, options.Command);
        Assert.Equal("2", options.DisplaySelector);
    }

    [Fact]
    public void DisplayOption_AcceptsEqualsForm()
    {
        Assert.Equal("Samsung", CommandLine.Parse(["off", "--display=Samsung"]).DisplaySelector);
    }

    [Fact]
    public void DisplayOption_AcceptsShortForm()
    {
        Assert.Equal("1", CommandLine.Parse(["toggle", "-d", "1"]).DisplaySelector);
    }

    [Fact]
    public void DisplayOption_WithoutValue_IsAnError()
    {
        var options = CommandLine.Parse(["on", "--display"]);

        Assert.NotNull(options.Error);
        Assert.Contains("--display", options.Error!);
    }

    [Fact]
    public void JsonAndQuietFlags_AreParsed()
    {
        var options = CommandLine.Parse(["status", "--json", "--quiet"]);

        Assert.True(options.Json);
        Assert.True(options.Quiet);
    }

    [Fact]
    public void AllAndDisplay_Contradict()
    {
        var options = CommandLine.Parse(["on", "--all", "--display", "2"]);

        Assert.NotNull(options.Error);
    }

    [Fact]
    public void TwoConflictingCommands_AreRejected()
    {
        var options = CommandLine.Parse(["on", "off"]);

        Assert.NotNull(options.Error);
        Assert.Contains("conflicts", options.Error!);
    }

    [Fact]
    public void UnknownArgument_IsRejectedRatherThanIgnored()
    {
        var options = CommandLine.Parse(["toggle", "--turbo"]);

        Assert.NotNull(options.Error);
        Assert.Contains("--turbo", options.Error!);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void HelpFlags_RequestHelp(string arg)
    {
        Assert.Equal(CliCommand.Help, CommandLine.Parse([arg]).Command);
    }

    [Fact]
    public void TrayFlag_KeepsTrayMode()
    {
        // The autostart Run key uses "--tray"; it must not be treated as an unknown argument.
        var options = CommandLine.Parse(["--tray"]);

        Assert.Equal(CliCommand.Tray, options.Command);
        Assert.Null(options.Error);
        Assert.False(options.IsConsoleCommand);
    }

    [Fact]
    public void ExitCodes_AreStable()
    {
        // Shortcuts and scripts depend on these; changing them is a breaking change.
        Assert.Equal(0, ExitCodes.Ok);
        Assert.Equal(1, ExitCodes.Error);
        Assert.Equal(2, ExitCodes.NoHdrCapableDisplay);
    }
}

public class ProcessMatchingTests
{
    [Theory]
    [InlineData("obs64", "obs64.exe")]
    [InlineData("obs64.exe", "obs64.exe")]
    [InlineData("OBS64.EXE", "obs64.exe")]
    [InlineData(@"C:\Program Files\obs-studio\bin\64bit\obs64.exe", "obs64.exe")]
    [InlineData("  Zoom  ", "zoom.exe")]
    public void NormalizeExeName_ReducesToALowercaseFileName(string input, string expected)
    {
        Assert.Equal(expected, ProcessHeuristic.NormalizeExeName(input));
    }

    [Fact]
    public void NormalizeExeName_OnEmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ProcessHeuristic.NormalizeExeName("   "));
    }

    [Fact]
    public void Heuristic_WithEmptyWatchList_MatchesNothing()
    {
        // The fallback is opt-in: an empty list must never produce a false alarm.
        Assert.Empty(ProcessHeuristic.Match(["obs64", "chrome"], []));
    }

    [Fact]
    public void Heuristic_MatchesWatchedProcesses()
    {
        var sessions = ProcessHeuristic.Match(["chrome", "obs64", "explorer"], ["obs64.exe"]);

        var session = Assert.Single(sessions);
        Assert.Equal("obs64.exe", session.AppKey);
        Assert.Equal(CaptureCapability.ProcessHeuristic, session.Capability);
        Assert.Equal("obs64", session.AppName);
    }

    [Fact]
    public void Heuristic_DeduplicatesMultipleInstances()
    {
        Assert.Single(ProcessHeuristic.Match(["obs64", "obs64", "obs64"], ["obs64"]));
    }

    [Fact]
    public void GameWatcher_MatchesEnabledRulesOnly()
    {
        var rules = new List<GameRule>
        {
            new() { ExeName = "cyberpunk2077.exe", Enabled = true },
            new() { ExeName = "doom.exe", Enabled = false },
        };

        var matches = GameWatcher.MatchRunning(["cyberpunk2077", "doom", "explorer"], rules);

        Assert.Equal(["cyberpunk2077.exe"], matches);
    }

    [Fact]
    public void GameWatcher_WithNoRules_MatchesNothing()
    {
        Assert.Empty(GameWatcher.MatchRunning(["cyberpunk2077"], []));
    }

    [Fact]
    public void GameWatcher_ToleratesRulesWrittenWithoutTheExtension()
    {
        var rules = new List<GameRule> { new() { ExeName = "cyberpunk2077", Enabled = true } };

        Assert.Contains("cyberpunk2077.exe", GameWatcher.MatchRunning(["Cyberpunk2077"], rules));
    }
}
