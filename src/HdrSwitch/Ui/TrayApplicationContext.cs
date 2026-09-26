using HdrSwitch.Core.Config;
using HdrSwitch.Core.Hdr;
using HdrSwitch.Core.Localization;
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
    private readonly UpdateFlow _updates;

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

    internal TrayApplicationContext(bool justUpdated = false)
    {
        _settings = _store.Load();
        _rules = new RuleEngine(_settings.AppRules);
        L.Use(_settings.Language);

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

        _updates = new UpdateFlow(exitApplication: ExitApplication);

        if (justUpdated)
        {
            UpdateFlow.AnnounceUpdated();
        }

        if (_store.LoadWarning is { } warning)
        {
            ToastWindow.ShowNotice("HDR Switch", warning, ShortNoticeSeconds + 4);
        }

        if (!_settings.IntroShown)
        {
            _settings.IntroShown = true;
            Save();
            ToastWindow.ShowNotice(
                L.T("HDR Switch is running"),
                L.F("Click the tray icon to flip HDR, or press {0}. If an app starts sharing your screen while HDR is on, you'll get a heads-up.", _settings.Hotkey),
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
        var sharing = ActiveShares;

        _tray.Icon = IconFactory.ForState(anyOn, capable.Count > 0, sharing.Count > 0);
        _tray.Text = BuildTrayTooltip(capable, anyOn, sharing);
        _settingsForm?.NotifyDisplaysChanged(_displays);
    }

    /// <summary>Apps confirmed to be sharing right now, as the capture watcher sees them.</summary>
    private IReadOnlyList<CaptureSession> ActiveShares => _captureWatcher?.ActiveSessions ?? [];

    private static string BuildTrayTooltip(
        IReadOnlyList<DisplayTarget> capable, bool anyOn, IReadOnlyList<CaptureSession> sharing)
    {
        if (sharing.Count > 0)
        {
            // NotifyIcon.Text is capped at 63 characters, and app names can be long.
            var who = sharing.Count == 1 ? sharing[0].AppName : L.F("{0} apps", sharing.Count);
            var text = anyOn ? L.F("{0} sharing · HDR on", who) : L.F("{0} sharing · HDR off", who);
            return text.Length <= 63 ? text : text[..60] + "...";
        }

        if (capable.Count == 0)
        {
            return L.T("HDR Switch — no HDR-capable display");
        }

        if (capable.Count == 1)
        {
            return capable[0].HdrEnabled
                ? L.F("HDR Switch — {0}: HDR on", capable[0].Label)
                : L.F("HDR Switch — {0}: HDR off", capable[0].Label);
        }

        var on = capable.Count(d => d.HdrEnabled);
        // NotifyIcon.Text is capped at 63 characters; keep it short rather than risk truncation.
        return on == 0 ? L.T("HDR Switch — HDR all off")
            : on == capable.Count ? L.T("HDR Switch — HDR all on")
            : L.F("HDR Switch — HDR {0} of {1} on", on, capable.Count);
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

        if (AddSharingItems(menu))
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        if (_displays.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem(L.T("No displays detected")) { Enabled = false });
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
            capable.Any(d => d.HdrEnabled) ? L.T("Turn all HDR off") : L.T("Turn all HDR on"))
        {
            Enabled = capable.Count > 0,
            ShortcutKeyDisplayString = _settings.HotkeyEnabled ? _settings.Hotkey : null,
        };
        toggleAll.Click += (_, _) => ToggleAll();
        menu.Items.Add(toggleAll);

        menu.Items.Add(new ToolStripSeparator());

        var watchSharing = new ToolStripMenuItem(L.T("Warn me when sharing my screen"))
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

        var settings = new ToolStripMenuItem(L.T("Settings…"));
        settings.Click += (_, _) => OpenSettings();
        menu.Items.Add(settings);

        var updates = new ToolStripMenuItem(L.T("Check for updates…"));
        updates.Click += (_, _) => _ = _updates.CheckNowAsync();
        menu.Items.Add(updates);

        if (_hotkeyWarning is { } warning)
        {
            var warn = new ToolStripMenuItem(warning) { Enabled = false };
            menu.Items.Add(warn);
        }

        var exit = new ToolStripMenuItem(L.T("Exit"));
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(exit);
    }

    /// <summary>
    /// One line per app that is sharing, with what HDR Switch did about it and the one action
    /// that undoes or completes it. Returns whether anything was added.
    /// </summary>
    private bool AddSharingItems(ContextMenuStrip menu)
    {
        var sharing = ActiveShares;

        foreach (var session in sharing)
        {
            var approximate = session.Capability == CaptureCapability.ProcessHeuristic;
            menu.Items.Add(new ToolStripMenuItem(approximate
                ? L.F("● {0} may be capturing your screen", session.AppName)
                : L.F("● {0} is sharing your screen", session.AppName))
            {
                Enabled = false,
            });

            if (_sharingRestore.TryGetValue(session.AppKey, out var turnedOff))
            {
                var labels = _displays.Where(d => turnedOff.Contains(d.StableId)).Select(d => d.Label).ToList();
                var restore = new ToolStripMenuItem(labels.Count > 0
                    ? L.F("Restore HDR now ({0})", string.Join(", ", labels))
                    : L.T("Restore HDR now"));
                var key = session.AppKey;
                restore.Click += (_, _) => RestoreAfterSharing(key, announce: true);
                menu.Items.Add(restore);
                continue;
            }

            var affected = _displays.Where(d => d.CanToggle && d.HdrEnabled).ToList();
            if (affected.Count > 0)
            {
                var turnOff = new ToolStripMenuItem(L.T("Turn HDR off now…"));
                var captured = session;
                turnOff.Click += (_, _) => ShowSharingSuggestion(captured, affected, approximate);
                menu.Items.Add(turnOff);
            }
        }

        return sharing.Count > 0;
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

        var item = new ToolStripMenuItem(L.T("Auto HDR for games"))
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
                    desired ? L.T("Auto HDR enabled") : L.T("Auto HDR disabled"),
                    L.T("Games that are already running need to be restarted before this takes effect."),
                    ShortNoticeSeconds + 2);
            }
            catch (Exception ex)
            {
                ToastWindow.ShowNotice(L.T("Could not change Auto HDR"), ex.Message, ShortNoticeSeconds + 2);
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
                L.T("No HDR-capable display"),
                blocked.Count > 0
                    ? L.F("HDR is blocked by system policy on {0}.", string.Join(", ", blocked.Select(d => d.Label)))
                    : L.T("None of the connected displays report HDR support."),
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
                L.T("HDR did not change"),
                string.Join("\n", failures.Select(f => f.Message ?? L.F("{0} failed.", f.Target.Label))),
                ShortNoticeSeconds + 4);
            return;
        }

        if (!_settings.ShowBalloonOnToggle)
        {
            return;
        }

        var names = string.Join(", ", results.Select(r => r.Target.Label));
        ToastWindow.ShowNotice(desired ? L.T("HDR on") : L.T("HDR off"), names, ShortNoticeSeconds);
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
                ToastWindow.ShowNotice(L.T("Screen-share detection degraded"), message, ShortNoticeSeconds + 4));
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
                var chosen = RuleEngine.SelectDisplays(
                    _rules.Find(session.AppKey), affected.Select(d => d.StableId).ToList());
                TurnOffForSharing(session, affected.Where(d => chosen.Contains(d.StableId)).ToList(), learned: true);
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
            ? L.F("{0} is running and may be capturing. HDR is on for {1}, which usually looks washed out and desaturated to whoever is watching.", session.AppName, displayNames)
            : affected.Count > 1
                ? L.F("HDR is on for {0} screens. Which one are you sharing? The other keeps HDR; captured HDR reaches viewers washed out.", affected.Count)
                : L.F("HDR is on for {0}. Captured HDR usually reaches viewers washed out and desaturated, because it gets flattened to SDR on the way.", displayNames);

        ToastWindow.ShowSuggestion(
            session.AppName,
            approximate ? L.F("{0} may be capturing your screen", session.AppName) : L.F("{0} is sharing your screen", session.AppName),
            detail,
            _settings.ToastSeconds,
            affected.Select(d => (d.StableId, d.Label)).ToList(),
            (answer, displayIds) =>
            {
                var state = _rules.RecordAnswer(session.AppKey, session.AppName, answer, displayIds);
                Save();

                if (answer == CaptureAnswer.TurnOff)
                {
                    var targets = displayIds is null
                        ? affected
                        : affected.Where(d => displayIds.Contains(d.StableId)).ToList();
                    TurnOffForSharing(session, targets, learned: false);

                    if (state == RuleState.AutoTurnOff)
                    {
                        ToastWindow.ShowNotice(
                            L.F("Learned: HDR off for {0}", session.AppName),
                            affected.Count > 1
                                ? L.T("Next time it shares your screen, HDR will switch off automatically on the screen you picked. You can change this in Settings.")
                                : L.T("Next time it shares your screen, HDR will switch off automatically. You can change this in Settings."),
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

        var switchedOff = string.Join(", ", affected.Where(d => turnedOff.Contains(d.StableId)).Select(d => d.Label));

        // An automatic action must always be reversible in one click, and the undo has to also
        // unlearn -- otherwise a rule learned by mistake can only be fixed from Settings.
        ToastWindow.ShowNotice(
            L.F("HDR off — {0} is sharing", session.AppName),
            _displays.Any(d => d.CanToggle && d.HdrEnabled)
                ? L.F("Switched off on {0}, other screens left alone, because that is what you chose before.", switchedOff)
                : L.F("Switched off on {0}, because that is what you chose before.", switchedOff),
            _settings.ToastSeconds,
            actionText: L.T("Undo and ask me next time"),
            onAction: () =>
            {
                _rules.Undo(session.AppKey);
                Save();
                RestoreAfterSharing(session.AppKey, announce: false);
                ToastWindow.ShowNotice(
                    L.F("HDR restored for {0}", session.AppName),
                    L.T("HDR Switch will ask again next time instead of deciding for you."),
                    ShortNoticeSeconds + 2);
            });
    }

    private void OnCaptureStopped(CaptureSession session)
    {
        // Drop the live dot whatever else happens.
        RefreshDisplays(updateIcon: true);

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
            L.F("{0} stopped sharing", appName),
            L.T("HDR is still off. Want it back on?"),
            _settings.ToastSeconds,
            actionText: L.T("Turn HDR back on"),
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
            ToastWindow.ShowNotice(L.T("HDR restored"), string.Join(", ", restored), ShortNoticeSeconds);
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
                L.F("HDR on for {0}", rule.DisplayName is { Length: > 0 } ? rule.DisplayName : rule.ExeName),
                L.T("It will go back off when the game exits."),
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
            ToastWindow.ShowNotice(L.T("Hotkey unavailable"), _hotkeyWarning, ShortNoticeSeconds + 4);
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
        _settingsForm.CheckForUpdatesRequested += (_, _) => _ = _updates.CheckNowAsync();
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void OnSettingsChanged(object? sender, AppSettings updated)
    {
        _settings = updated;
        _rules = new RuleEngine(_settings.AppRules);
        L.Use(_settings.Language);
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
            _updates.Dispose();
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
