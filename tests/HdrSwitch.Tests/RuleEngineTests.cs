using HdrSwitch.Core.Rules;
using Xunit;

namespace HdrSwitch.Tests;

public class RuleEngineTests
{
    private static RuleEngine NewEngine() => new(new List<AppRule>());

    [Fact]
    public void UnknownApp_DefaultsToAsking()
    {
        var engine = NewEngine();
        Assert.Equal(CaptureDecision.Ask, engine.Decide("discord.exe"));
    }

    [Fact]
    public void TwoConsistentTurnOffAnswers_PromotesToAutomatic()
    {
        var engine = NewEngine();

        Assert.Equal(RuleState.Ask, engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff));
        Assert.Equal(CaptureDecision.Ask, engine.Decide("discord.exe"));

        Assert.Equal(RuleState.AutoTurnOff, engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff));
        Assert.Equal(CaptureDecision.TurnOffAutomatically, engine.Decide("discord.exe"));
    }

    [Fact]
    public void TwoConsistentKeepAnswers_PromotesToAutoKeep()
    {
        var engine = NewEngine();

        engine.RecordAnswer("obs64.exe", "OBS", CaptureAnswer.Keep);
        Assert.Equal(RuleState.AutoKeep, engine.RecordAnswer("obs64.exe", "OBS", CaptureAnswer.Keep));
        Assert.Equal(CaptureDecision.DoNothing, engine.Decide("obs64.exe"));
    }

    [Fact]
    public void MixedAnswers_NeverPromote()
    {
        var engine = NewEngine();

        engine.RecordAnswer("chrome.exe", "Chrome", CaptureAnswer.TurnOff);
        engine.RecordAnswer("chrome.exe", "Chrome", CaptureAnswer.Keep);
        engine.RecordAnswer("chrome.exe", "Chrome", CaptureAnswer.TurnOff);
        engine.RecordAnswer("chrome.exe", "Chrome", CaptureAnswer.TurnOff);

        // Three turn-offs, but a contradicting answer exists, so the user keeps being asked.
        Assert.Equal(CaptureDecision.Ask, engine.Decide("chrome.exe"));
    }

    [Fact]
    public void NeverAsk_TakesEffectImmediately()
    {
        var engine = NewEngine();

        Assert.Equal(RuleState.AutoKeep, engine.RecordAnswer("teams.exe", "Teams", CaptureAnswer.NeverAsk));
        Assert.Equal(CaptureDecision.DoNothing, engine.Decide("teams.exe"));
    }

    [Fact]
    public void Undo_DemotesToAskAndClearsCounters()
    {
        var engine = NewEngine();
        engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff);
        engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff);
        Assert.Equal(CaptureDecision.TurnOffAutomatically, engine.Decide("discord.exe"));

        engine.Undo("discord.exe");

        Assert.Equal(CaptureDecision.Ask, engine.Decide("discord.exe"));

        var rule = engine.Find("discord.exe");
        Assert.NotNull(rule);
        Assert.Equal(0, rule!.TurnOffCount);
        Assert.Equal(0, rule.KeepCount);
    }

    [Fact]
    public void Undo_ThenOneAnswer_DoesNotImmediatelyRelearn()
    {
        // Regression guard: if Undo only reset the state and not the counters, a single answer
        // would re-promote instantly and the undo would feel like it did nothing.
        var engine = NewEngine();
        engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff);
        engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff);
        engine.Undo("discord.exe");

        engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.TurnOff);

        Assert.Equal(CaptureDecision.Ask, engine.Decide("discord.exe"));
    }

    [Fact]
    public void AppKey_IsCaseInsensitive()
    {
        var engine = NewEngine();
        engine.RecordAnswer("Discord.exe", "Discord", CaptureAnswer.NeverAsk);

        Assert.Equal(CaptureDecision.DoNothing, engine.Decide("discord.EXE"));
    }

    [Fact]
    public void SetState_ClearsLearnedCounters()
    {
        var engine = NewEngine();
        engine.RecordAnswer("zoom.exe", "Zoom", CaptureAnswer.TurnOff);

        engine.SetState("zoom.exe", "Zoom", RuleState.AutoKeep);

        var rule = engine.Find("zoom.exe")!;
        Assert.Equal(RuleState.AutoKeep, rule.State);
        Assert.Equal(0, rule.TurnOffCount);
    }

    [Fact]
    public void GetOrCreate_RefreshesDisplayNameButKeepsLearning()
    {
        var engine = NewEngine();
        engine.RecordAnswer("discord.exe", "discord", CaptureAnswer.TurnOff);

        engine.GetOrCreate("discord.exe", "Discord");

        var rule = engine.Find("discord.exe")!;
        Assert.Equal("Discord", rule.DisplayName);
        Assert.Equal(1, rule.TurnOffCount);
    }

    [Fact]
    public void Remove_ForgetsTheApp()
    {
        var engine = NewEngine();
        engine.RecordAnswer("discord.exe", "Discord", CaptureAnswer.NeverAsk);

        Assert.True(engine.Remove("discord.exe"));
        Assert.Equal(CaptureDecision.Ask, engine.Decide("discord.exe"));
    }
}
