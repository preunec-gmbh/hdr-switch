using HdrSwitch.Core.Config;
using HdrSwitch.Core.Hdr;
using HdrSwitch.Core.Rules;
using HdrSwitch.Core.Sharing;

namespace HdrSwitch.Ui;

/// <summary>
/// The tray application: owns the icon, the menu, the global hotkey, and the two watchers.
///
/// All watcher callbacks arrive on background threads and are marshalled onto the UI thread
/// before touching any state, so the rule engine and settings are only ever mutated from one
/// thread.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int RefreshIntervalMs = 5000;
    private const int ShortNoticeSeconds = 4;

    private readonly SettingsStore _store = new();
    private readonly HdrController _hdr = new();
    private readonly ProcessHeuristic _heuristic = new();
    private readonly Control _marshal = new();
    private readonly NotifyIcon _tray;
    private readonly MessageWindow _window;
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    /// <summary>Displays we switched off for a given capturing app, so they can be restored.</summary>
    private readonly Dictionary<string, List<string>> _sharingRestore = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Displays we switched on for a given game, so they can be put back.</summary>
    private readonly Dictionary<string, List<string>> _gameRestore = new(StringComparer.OrdinalIgnoreCase);

    private AppSettings _settings;
    private RuleEngine _rules;
    private CaptureWatcher? _captureWatcher;
    private GameWatcher? _gameWatcher;
    private SettingsForm? _settingsForm;
    private IReadOnlyList<DisplayTarget> _displays = [];
    private string? _hotkeyWarning;

    internal TrayApplicationContext()
    {
        _settings = _store.Load();
        _rules = new RuleEngine(_settings.AppRules);

        // Forces handle creation so background threads have something to marshal onto.
        _ = _marshal.Handle;

        _tray = new NotifyIcon
        {
            Visible = true,
            Text = "HDR Switch",
            ContextMenuStrip = new ContextMenuStrip { ShowImageMargin = false },
        };
        _tray.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _tray.MouseClick += OnTrayClick;

        _window = new MessageWindow();
        _window.HotkeyPressed += () => Marshal(ToggleAll);
        _window.DisplayConfigurationChanged += () => Marshal(() => RefreshDisplays(updateIcon: true));
        _window.ShowSettingsRequested += () => Marshal(OpenSettings);

        RefreshDisplays(updateIcon: true);
        ApplyHotkey();
        StartWatchers();

        _refreshTimer.Interval = RefreshIntervalMs;
        _refreshTimer.Tick += (_, _) => RefreshDisplays(updateIcon: true);
        _refreshTimer.Start();

        if (_store.LoadWarning is { } warning)
        {
            ToastWindow.ShowNotice("HDR Switch", warning, ShortNoticeSeconds + 4);
        }

        if (!_settings.IntroShown)
        {
            _settings.IntroShown = true;
            Save();
            ToastWindow.ShowNotice(
                "HDR Switch is running",
                "Click the tray icon to flip HDR, or press " + _settings.Hotkey + ". " +
                "If an app starts sharing your screen while HDR is on, you'll get a heads-up.",
                10);
        }
    }

    // ---------------------------------------------------------------- state

    private void Marshal(Action action)
    {
        if (_marshal.IsDisposed)
        {
            return;
        }

        try
        {
            if (_marshal.InvokeRequired)
            {
                _marshal.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
    }

    private void RefreshDisplays(bool updateIcon)
    {
        _displays = _hdr.GetDisplays();

        if (!updateIcon)
        {
            return;
        }

        var capable = _displays.Where(d => d.CanToggle).ToList();
        var anyOn = capable.Any(d => d.HdrEnabled);

        _tray.Icon = IconFactory.ForState(anyOn, capable.Count > 0);
        _tray.Text = BuildTrayTooltip(capable, anyOn);
        _settingsForm?.NotifyDisplaysChanged(_displays);
    }

    private static string BuildTrayTooltip(IReadOnlyList<DisplayTarget> capable, bool anyOn)
    {
        if (capable.Count == 0)
        {
            return "HDR Switch — no HDR-capable display";
        }

        if (capable.Count == 1)
        {
            return $"HDR Switch — {capable[0].Label}: HDR {(capable[0].HdrEnabled ? "on" : "off")}";
        }

        var on = capable.Count(d => d.HdrEnabled);
        var summary = on == 0 ? "all off" : on == capable.Count ? "all on" : $"{on} of {capable.Count} on";

        // NotifyIcon.Text is capped at 63 characters; keep it short rather than risk truncation.
        return $"HDR Switch — HDR {summary}";
    }

    private void Save() => _store.Save(_settings);

    // ---------------------------------------------------------------- menu

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        // Left click is the fast path: flip everything. The menu is on right click.
        if (e.Button == MouseButtons.Left)
        {
            ToggleAll();
        }
    }

    private void BuildMenu()
    {
        var menu = _tray.ContextMenuStrip!;
        menu.Items.Clear();

        RefreshDisplays(updateIcon: true);

        if (_displays.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("No displays detected") { Enabled = false });
        }

        foreach (var display in _displays)
        {
            var item = new ToolStripMenuItem($"{display.Label}  —  {display.StatusText}")
            {
                Checked = display.HdrEnabled,
                CheckOnClick = false,
                Enabled = display.CanToggle,
            };

            var captured = display;
            item.Click += (_, _) => SetDisplay(captured, !captured.HdrEnabled);
            menu.Items.Add(item);
        }

        menu.Items.Add(new ToolStripSeparator());

        var capable = _displays.Where(d => d.CanToggle).ToList();
        var toggleAll = new ToolStripMenuItem(
            capable.Any(d => d.HdrEnabled) ? "Turn all HDR off" : "Turn all HDR on")
        {
            Enabled = capable.Count > 0,
            ShortcutKeyDisplayString = _settings.HotkeyEnabled ? _settings.Hotkey : null,
        };
        toggleAll.Click += (_, _) => ToggleAll();
        menu.Items.Add(toggleAll);

        menu.Items.Add(new ToolStripSeparator());

        var watchSharing = new ToolStripMenuItem("Warn me when sharing my screen")
        {
            Checked = _settings.WatchScreenSharing,
        };

        // Deliberately Click rather than CheckedChanged with CheckOnClick: the menu is rebuilt on
        // every open and its Checked state is assigned from settings, so a change-based handler
        // can persist a value nobody chose. Only a real click writes.
        watchSharing.Click += (_, _) =>
        {
            _settings.WatchScreenSharing = !_settings.WatchScreenSharing;
            Save();
            StartWatchers();
        };
        menu.Items.Add(watchSharing);

        var autoHdr = BuildAutoHdrItem();
        if (autoHdr is not null)
        {
            menu.Items.Add(autoHdr);
        }

        menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem("Settings…");
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        if (_hotkeyWarning is { } warning)
        {
            var warn = new ToolStripMenuItem(warning) { Enabled = false };
            menu.Items.Add(warn);
        }

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(exit);
    }

    private ToolStripMenuItem? BuildAutoHdrItem()
    {
        bool? current;
        try
        {
            current = AutoHdrSettings.IsEnabled();
        }
        catch (Exception)
        {
            return null;
        }

        if (current is null)
        {
            // Windows has not recorded a preference; do not invent one in the menu.
            return null;
        }

        var item = new ToolStripMenuItem("Auto HDR for games")
        {
            Checked = current.Value,
        };

        item.Click += (_, _) =>
        {
            try
            {
                var desired = !current.Value;
                AutoHdrSettings.SetEnabled(desired);
                ToastWindow.ShowNotice(
                    $"Auto HDR {(desired ? "enabled" : "disabled")}",
                    "Games that are already running need to be restarted before this takes effect.",
                    ShortNoticeSeconds + 2);
            }
            catch (Exception ex)
            {
                ToastWindow.ShowNotice("Could not change Auto HDR", ex.Message, ShortNoticeSeconds + 2);
            }
        };

        return item;
    }

    // ---------------------------------------------------------------- toggling

    private void ToggleAll()
    {
        RefreshDisplays(updateIcon: false);
        var capable = _displays.Where(d => d.CanToggle).ToList();

        if (capable.Count == 0)
        {
            var blocked = _displays.Where(d => d.Capability == HdrCapability.BlockedByPolicy).ToList();
            ToastWindow.ShowNotice(
                "No HDR-capable display",
                blocked.Count > 0
                    ? $"HDR is blocked by system policy on {string.Join(", ", blocked.Select(d => d.Label))}."
                    : "None of the connected displays report HDR support.",
                ShortNoticeSeconds + 2);
            return;
        }

        // Use "is anything on" as the reference so a mixed set converges rather than inverting
        // each display independently.
        var desired = !capable.Any(d => d.HdrEnabled);
        var results = capable.Select(d => _hdr.SetHdr(d, desired)).ToList();

        RefreshDisplays(updateIcon: true);
        ReportResults(results, desired);
    }

    private void SetDisplay(DisplayTarget display, bool enable)
    {
        var result = _hdr.SetHdr(display, enable);
        RefreshDisplays(updateIcon: true);
        ReportResults([result], enable);
    }

    private void ReportResults(IReadOnlyList<HdrSetResult> results, bool desired)
    {
        var failures = results.Where(r => !r.Success).ToList();

        if (failures.Count > 0)
        {
            ToastWindow.ShowNotice(
                "HDR did not change",
                string.Join("\n", failures.Select(f => f.Message ?? $"{f.Target.Label} failed.")),
                ShortNoticeSeconds + 4);
            return;
        }

        if (!_settings.ShowBalloonOnToggle)
        {
            return;
        }

        var names = string.Join(", ", results.Select(r => r.Target.Label));
        ToastWindow.ShowNotice($"HDR {(desired ? "on" : "off")}", names, ShortNoticeSeconds);
    }

    // ---------------------------------------------------------------- watchers

    private void StartWatchers()
    {
        if (_settings.WatchScreenSharing && _captureWatcher is null)
        {
            _captureWatcher = new CaptureWatcher(
                heuristicProvider: () => _settings.ProcessHeuristicEnabled
                    ? _heuristic.Detect(_settings.ProcessWatchList)
                    : []);
            _captureWatcher.CaptureStarted += (_, session) => Marshal(() => OnCaptureStarted(session));
            _captureWatcher.CaptureStopped += (_, session) => Marshal(() => OnCaptureStopped(session));
            _captureWatcher.Degraded += (_, message) => Marshal(() =>
                ToastWindow.ShowNotice("Screen-share detection degraded", message, ShortNoticeSeconds + 4));
            _captureWatcher.Start();
        }
        else if (!_settings.WatchScreenSharing && _captureWatcher is not null)
        {
            _captureWatcher.Dispose();
            _captureWatcher = null;
        }

        if (_settings.WatchGames && _gameWatcher is null)
        {
            _gameWatcher = new GameWatcher(() => _settings.GameRules);
            _gameWatcher.GameStarted += (_, rule) => Marshal(() => OnGameStarted(rule));
            _gameWatcher.GameStopped += (_, rule) => Marshal(() => OnGameStopped(rule));
            _gameWatcher.Start();
        }
        else if (!_settings.WatchGames && _gameWatcher is not null)
        {
            _gameWatcher.Dispose();
            _gameWatcher = null;
        }
    }

    private void OnCaptureStarted(CaptureSession session)
    {
        if (!_settings.WatchScreenSharing)
        {
            return;
        }

        RefreshDisplays(updateIcon: true);
        var affected = _displays.Where(d => d.CanToggle && d.HdrEnabled).ToList();

        // Nothing to warn about: HDR is already off everywhere.
        if (affected.Count == 0)
        {
            return;
        }

        var approximate = session.Capability == CaptureCapability.ProcessHeuristic;

        switch (_rules.Decide(session.AppKey))
        {
            case CaptureDecision.DoNothing:
                return;

            case CaptureDecision.TurnOffAutomatically:
                TurnOffForSharing(session, affected, learned: true);
                return;

            default:
                ShowSharingSuggestion(session, affected, approximate);
                return;
        }
    }

    private void ShowSharingSuggestion(CaptureSession session, IReadOnlyList<DisplayTarget> affected, bool approximate)
    {
        var displayNames = string.Join(", ", affected.Select(d => d.Label));
        var detail = approximate
            ? $"{session.AppName} is running and may be capturing. HDR is on for {displayNames}, " +
              "which usually looks washed out and desaturated to whoever is watching."
            : $"HDR is on for {displayNames}. Captured HDR usually reaches viewers washed out " +
              "and desaturated, because it gets flattened to SDR on the way.";

        ToastWindow.ShowSuggestion(
            session.AppName,
            $"{session.AppName} is capturing your screen",
            detail,
            _settings.ToastSeconds,
            answer =>
            {
                var state = _rules.RecordAnswer(session.AppKey, session.AppName, answer);
                Save();

                if (answer == CaptureAnswer.TurnOff)
                {
                    TurnOffForSharing(session, affected, learned: false);

                    if (state == RuleState.AutoTurnOff)
                    {
                        ToastWindow.ShowNotice(
                            $"Learned: HDR off for {session.AppName}",
                            "Next time it shares your screen, HDR will switch off automatically. " +
                            "You can change this in Settings.",
                            ShortNoticeSeconds + 3);
                    }
                }
            });
    }

    private void TurnOffForSharing(CaptureSession session, IReadOnlyList<DisplayTarget> affected, bool learned)
    {
        var turnedOff = new List<string>();

        foreach (var display in affected)
        {
            var result = _hdr.SetHdr(display, false);
            if (result.Success)
            {
                turnedOff.Add(display.StableId);
            }
        }

        RefreshDisplays(updateIcon: true);

        if (turnedOff.Count == 0)
        {
            return;
        }

        _sharingRestore[session.AppKey] = turnedOff;

        if (!learned)
        {
            return;
        }

        // An automatic action must always be reversible in one click, and the undo has to also
        // unlearn -- otherwise a rule learned by mistake can only be fixed from Settings.
        ToastWindow.ShowNotice(
            "HDR off — " + session.AppName + " is sharing",
            "HDR Switch did this automatically because that is what you chose before.",
            _settings.ToastSeconds,
            actionText: "Undo and ask me next time",
            onAction: () =>
            {
                _rules.Undo(session.AppKey);
                Save();
                RestoreAfterSharing(session.AppKey, announce: false);
                ToastWindow.ShowNotice(
                    $"HDR restored for {session.AppName}",
                    "HDR Switch will ask again next time instead of deciding for you.",
                    ShortNoticeSeconds + 2);
            });
    }

    private void OnCaptureStopped(CaptureSession session)
    {
        if (!_sharingRestore.ContainsKey(session.AppKey))
        {
            return;
        }

        if (!_settings.RestoreHdrAfterSharing)
        {
            _sharingRestore.Remove(session.AppKey);
            return;
        }

        // When the choice was learned, restore silently. When it was a one-off answer, offer it
        // rather than acting on the user's behalf a second time.
        if (_rules.Decide(session.AppKey) == CaptureDecision.TurnOffAutomatically)
        {
            RestoreAfterSharing(session.AppKey, announce: true);
            return;
        }

        var appName = session.AppName;
        var key = session.AppKey;

        ToastWindow.ShowNotice(
            $"{appName} stopped sharing",
            "HDR is still off. Want it back on?",
            _settings.ToastSeconds,
            actionText: "Turn HDR back on",
            onAction: () => RestoreAfterSharing(key, announce: false));
    }

    private void RestoreAfterSharing(string appKey, bool announce)
    {
        if (!_sharingRestore.Remove(appKey, out var displayIds))
        {
            return;
        }

        RefreshDisplays(updateIcon: false);
        var restored = new List<string>();

        foreach (var id in displayIds)
        {
            var display = _displays.FirstOrDefault(d => d.StableId == id);

            // Only restore what is still off. If the user switched it back on themselves in the
            // meantime, there is nothing to do.
            if (display is null || !display.CanToggle || display.HdrEnabled)
            {
                continue;
            }

            if (_hdr.SetHdr(display, true).Success)
            {
                restored.Add(display.Label);
            }
        }

        RefreshDisplays(updateIcon: true);

        if (announce && restored.Count > 0)
        {
            ToastWindow.ShowNotice("HDR restored", string.Join(", ", restored), ShortNoticeSeconds);
        }
    }

    // ---------------------------------------------------------------- game rules

    private void OnGameStarted(GameRule rule)
    {
        RefreshDisplays(updateIcon: false);

        var targets = ResolveRuleDisplays(rule).Where(d => !d.HdrEnabled).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var turnedOn = new List<string>();
        foreach (var display in targets)
        {
            if (_hdr.SetHdr(display, true).Success)
            {
                turnedOn.Add(display.StableId);
            }
        }

        RefreshDisplays(updateIcon: true);

        if (turnedOn.Count > 0)
        {
            _gameRestore[rule.ExeName] = turnedOn;
            ToastWindow.ShowNotice(
                "HDR on for " + (rule.DisplayName is { Length: > 0 } ? rule.DisplayName : rule.ExeName),
                "It will go back off when the game exits.",
                ShortNoticeSeconds);
        }
    }

    private void OnGameStopped(GameRule rule)
    {
        if (!_gameRestore.Remove(rule.ExeName, out var displayIds))
        {
            return;
        }

        RefreshDisplays(updateIcon: false);

        foreach (var id in displayIds)
        {
            var display = _displays.FirstOrDefault(d => d.StableId == id);
            if (display is not null && display.CanToggle && display.HdrEnabled)
            {
                _hdr.SetHdr(display, false);
            }
        }

        RefreshDisplays(updateIcon: true);
    }

    private IReadOnlyList<DisplayTarget> ResolveRuleDisplays(GameRule rule)
    {
        var capable = _displays.Where(d => d.CanToggle).ToList();

        return rule.DisplayIds.Count == 0
            ? capable
            : capable.Where(d => rule.DisplayIds.Contains(d.StableId, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    // ---------------------------------------------------------------- hotkey & settings

    private void ApplyHotkey()
    {
        _hotkeyWarning = null;
        _window.UnregisterHotkey();

        if (!_settings.HotkeyEnabled)
        {
            return;
        }

        if (!HotkeyParser.TryParse(_settings.Hotkey, out var hotkey, out var parseError) || hotkey is null)
        {
            _hotkeyWarning = parseError;
            return;
        }

        _hotkeyWarning = _window.TryRegisterHotkey(hotkey);

        if (_hotkeyWarning is not null)
        {
            ToastWindow.ShowNotice("Hotkey unavailable", _hotkeyWarning, ShortNoticeSeconds + 4);
        }
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings, _rules, _displays);
        _settingsForm.SettingsChanged += OnSettingsChanged;
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnSettingsChanged(object? sender, AppSettings updated)
    {
        _settings = updated;
        _rules = new RuleEngine(_settings.AppRules);
        Save();

        ApplyHotkey();
        StartWatchers();
        RefreshDisplays(updateIcon: true);
    }

    private void ExitApplication()
    {
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _captureWatcher?.Dispose();
            _gameWatcher?.Dispose();
            _window.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _marshal.Dispose();
            IconFactory.Dispose();
        }

        base.Dispose(disposing);
    }
}
