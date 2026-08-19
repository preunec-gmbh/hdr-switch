namespace HdrSwitch.Core.Rules;

/// <summary>What HDR Switch should do when a given app starts capturing.</summary>
public enum RuleState
{
    /// <summary>Show the suggestion toast and wait for the user.</summary>
    Ask = 0,

    /// <summary>Learned: turn HDR off automatically, with an undo affordance.</summary>
    AutoTurnOff = 1,

    /// <summary>Learned or declared: leave HDR alone and stay quiet.</summary>
    AutoKeep = 2,
}

/// <summary>The user's response to a suggestion toast.</summary>
public enum CaptureAnswer
{
    TurnOff,
    Keep,
    NeverAsk,
}

/// <summary>What the tray host should actually do.</summary>
public enum CaptureDecision
{
    Ask,
    TurnOffAutomatically,
    DoNothing,
}

public sealed class AppRule
{
    /// <summary>Executable file name, lowercased. Survives app updates that change the install path.</summary>
    public string AppKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public RuleState State { get; set; } = RuleState.Ask;

    public int TurnOffCount { get; set; }

    public int KeepCount { get; set; }
}

/// <summary>
/// "Suggest, then learn": ask the first couple of times an app shares the screen, then apply the
/// answer the user kept giving.
///
/// Promotion requires <see cref="LearnThreshold"/> consistent answers with no contradicting
/// answer, so one accidental click never teaches the wrong lesson. Undo demotes all the way back
/// to <see cref="RuleState.Ask"/> and clears the counters -- otherwise a rule learned by mistake
/// could only be corrected by opening Settings, which is exactly when the user is least able to.
/// </summary>
public sealed class RuleEngine
{
    public const int LearnThreshold = 2;

    private readonly IList<AppRule> _rules;

    public RuleEngine(IList<AppRule> rules) => _rules = rules;

    public IReadOnlyList<AppRule> Rules => _rules.AsReadOnly();

    public AppRule? Find(string appKey) =>
        _rules.FirstOrDefault(r => string.Equals(r.AppKey, appKey, StringComparison.OrdinalIgnoreCase));

    public AppRule GetOrCreate(string appKey, string displayName)
    {
        var existing = Find(appKey);
        if (existing is not null)
        {
            // Keep the friendly name fresh; it can improve once the exe is readable.
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                existing.DisplayName = displayName;
            }

            return existing;
        }

        var rule = new AppRule
        {
            AppKey = appKey.ToLowerInvariant(),
            DisplayName = displayName,
            State = RuleState.Ask,
        };
        _rules.Add(rule);
        return rule;
    }

    public CaptureDecision Decide(string appKey) => Find(appKey)?.State switch
    {
        RuleState.AutoTurnOff => CaptureDecision.TurnOffAutomatically,
        RuleState.AutoKeep => CaptureDecision.DoNothing,
        _ => CaptureDecision.Ask,
    };

    /// <summary>Records an answer and returns the rule state after any promotion.</summary>
    public RuleState RecordAnswer(string appKey, string displayName, CaptureAnswer answer)
    {
        var rule = GetOrCreate(appKey, displayName);

        switch (answer)
        {
            case CaptureAnswer.NeverAsk:
                // An explicit instruction, not something to be learned gradually.
                rule.State = RuleState.AutoKeep;
                break;

            case CaptureAnswer.TurnOff:
                rule.TurnOffCount++;
                if (rule.TurnOffCount >= LearnThreshold && rule.KeepCount == 0)
                {
                    rule.State = RuleState.AutoTurnOff;
                }

                break;

            case CaptureAnswer.Keep:
                rule.KeepCount++;
                if (rule.KeepCount >= LearnThreshold && rule.TurnOffCount == 0)
                {
                    rule.State = RuleState.AutoKeep;
                }

                break;
        }

        return rule.State;
    }

    /// <summary>
    /// Undo an automatic action: forget what was learned and go back to asking.
    /// </summary>
    public void Undo(string appKey)
    {
        var rule = Find(appKey);
        if (rule is null)
        {
            return;
        }

        rule.State = RuleState.Ask;
        rule.TurnOffCount = 0;
        rule.KeepCount = 0;
    }

    /// <summary>Set a rule directly from the Settings UI, clearing learned counters.</summary>
    public void SetState(string appKey, string displayName, RuleState state)
    {
        var rule = GetOrCreate(appKey, displayName);
        rule.State = state;
        rule.TurnOffCount = 0;
        rule.KeepCount = 0;
    }

    public bool Remove(string appKey)
    {
        var rule = Find(appKey);
        return rule is not null && _rules.Remove(rule);
    }
}
