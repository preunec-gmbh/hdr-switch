using HdrSwitch.Core.Rules;

namespace HdrSwitch.Core.Config;

/// <summary>Turn HDR on while a particular game or app is running, then put it back.</summary>
public sealed class GameRule
{
    /// <summary>Executable file name, e.g. "cyberpunk2077.exe". Lowercased.</summary>
    public string ExeName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>Stable display ids to act on. Empty means every HDR-capable display.</summary>
    public List<string> DisplayIds { get; set; } = [];
}

/// <summary>Everything HDR Switch persists, in %APPDATA%\HdrSwitch\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Schema version, so a future format change can migrate rather than discard.</summary>
    public int Version { get; set; } = 1;

    // --- screen-sharing awareness ---
    public bool WatchScreenSharing { get; set; } = true;

    /// <summary>Restore HDR when the capture that caused us to disable it ends.</summary>
    public bool RestoreHdrAfterSharing { get; set; } = true;

    public List<AppRule> AppRules { get; set; } = [];

    /// <summary>Coarse process-name fallback. Off by default; see ProcessHeuristic.</summary>
    public bool ProcessHeuristicEnabled { get; set; }

    public List<string> ProcessWatchList { get; set; } = [];

    // --- hotkey ---
    public bool HotkeyEnabled { get; set; } = true;

    public string Hotkey { get; set; } = "Ctrl+Alt+H";

    // --- game rules ---
    public bool WatchGames { get; set; }

    public List<GameRule> GameRules { get; set; } = [];

    // --- presentation ---
    public bool ShowBalloonOnToggle { get; set; } = true;

    public int ToastSeconds { get; set; } = 20;

    /// <summary>Set once the first-run explanation has been shown.</summary>
    public bool IntroShown { get; set; }

    public AppSettings Clone() => new()
    {
        Version = Version,
        WatchScreenSharing = WatchScreenSharing,
        RestoreHdrAfterSharing = RestoreHdrAfterSharing,
        AppRules = AppRules.Select(r => new AppRule
        {
            AppKey = r.AppKey,
            DisplayName = r.DisplayName,
            State = r.State,
            TurnOffCount = r.TurnOffCount,
            KeepCount = r.KeepCount,
        }).ToList(),
        ProcessHeuristicEnabled = ProcessHeuristicEnabled,
        ProcessWatchList = [.. ProcessWatchList],
        HotkeyEnabled = HotkeyEnabled,
        Hotkey = Hotkey,
        WatchGames = WatchGames,
        GameRules = GameRules.Select(g => new GameRule
        {
            ExeName = g.ExeName,
            DisplayName = g.DisplayName,
            Enabled = g.Enabled,
            DisplayIds = [.. g.DisplayIds],
        }).ToList(),
        ShowBalloonOnToggle = ShowBalloonOnToggle,
        ToastSeconds = ToastSeconds,
        IntroShown = IntroShown,
    };
}
