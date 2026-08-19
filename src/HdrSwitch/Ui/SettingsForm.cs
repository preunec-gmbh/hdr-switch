using HdrSwitch.Core.Config;
using HdrSwitch.Core.Hdr;
using HdrSwitch.Core.Rules;
using HdrSwitch.Core.Sharing;

namespace HdrSwitch.Ui;

/// <summary>
/// Settings, including the editor for everything the app has learned. Anything HDR Switch decides
/// on its own has to be inspectable and reversible here, otherwise the learning behaviour is just
/// unpredictable.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _working;
    private readonly RuleEngine _rules;
    private IReadOnlyList<DisplayTarget> _displays;

    private readonly CheckBox _startWithWindows = new();
    private readonly CheckBox _showNoticeOnToggle = new();
    private readonly CheckBox _hotkeyEnabled = new();
    private readonly TextBox _hotkeyText = new();
    private readonly Label _hotkeyStatus = new();

    private readonly CheckBox _watchSharing = new();
    private readonly CheckBox _restoreAfter = new();
    private readonly NumericUpDown _toastSeconds = new();
    private readonly ListView _rulesList = new();

    private readonly CheckBox _watchGames = new();
    private readonly ListView _gamesList = new();

    private readonly CheckBox _heuristicEnabled = new();
    private readonly TextBox _heuristicList = new();
    private readonly Label _diagnostics = new();

    /// <summary>Panels whose height can only be resolved once the page has a real size.</summary>
    private readonly List<TableLayoutPanel> _stackedPanels = [];

    internal SettingsForm(AppSettings settings, RuleEngine rules, IReadOnlyList<DisplayTarget> displays)
    {
        // Edit a copy: closing without saving must not mutate the live settings.
        _working = settings.Clone();
        _rules = new RuleEngine(_working.AppRules);
        _ = rules;
        _displays = displays;

        Text = "HDR Switch — Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ClientSize = new Size(620, 590);
        MinimumSize = new Size(560, 520);
        Icon = IconFactory.App;
        Font = Brand.Body(9f);
        BackColor = Brand.SurfaceBase;
        ForeColor = Brand.TextPrimary;

        var header = new BrandHeader { Dock = DockStyle.Top, Height = 64 };

        var tabs = new BrandTabControl
        {
            Dock = DockStyle.Fill,
            // WinForms paints the tab strip with system colours no matter what the form says,
            // so on a navy surface it stays stubbornly light unless it is drawn by hand.
            DrawMode = TabDrawMode.OwnerDrawFixed,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(112, 28),
        };
        tabs.DrawItem += DrawTab;
        tabs.TabPages.Add(BuildGeneralTab());
        tabs.TabPages.Add(BuildSharingTab());
        tabs.TabPages.Add(BuildGamesTab());
        tabs.TabPages.Add(BuildAdvancedTab());

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(10),
        };

        var close = new Button { Text = "Close", Width = 90, Height = 28, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();

        var save = new Button
        {
            Text = "Save",
            Width = 90,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = Brand.AccentInteractive,
            ForeColor = Brand.AccentInteractiveFg,
        };
        save.FlatAppearance.BorderColor = Brand.AccentInteractive;
        save.Click += (_, _) => Apply();

        buttons.Controls.Add(close);
        buttons.Controls.Add(save);

        Controls.Add(tabs);
        Controls.Add(buttons);
        Controls.Add(header);
        AcceptButton = save;
        CancelButton = close;

        buttons.BackColor = Brand.SurfaceBase;

        LoadValues();
        ApplyBrandColours(this);
        UseDarkTitleBarWhenSystemIsDark();
    }

    private static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs || e.Index < 0 || e.Index >= tabs.TabPages.Count)
        {
            return;
        }

        var selected = e.Index == tabs.SelectedIndex;
        var bounds = e.Bounds;

        using (var background = new SolidBrush(selected ? Brand.SurfaceBase : Brand.SurfaceRaised))
        {
            e.Graphics.FillRectangle(background, bounds);
        }

        if (selected)
        {
            // Cyan underline marks the active tab. Cyan is a fill role, never the label colour.
            using var indicator = new SolidBrush(Brand.AccentInteractive);
            e.Graphics.FillRectangle(indicator, bounds.Left, bounds.Bottom - 2, bounds.Width, 2);
        }

        TextRenderer.DrawText(
            e.Graphics,
            tabs.TabPages[e.Index].Text,
            tabs.Font,
            bounds,
            selected ? Brand.TextPrimary : Brand.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    /// <summary>
    /// Windows 11 only repaints the title bar dark if the app asks. Without this the window has
    /// a white caption sitting above a navy surface, which looks broken rather than themed.
    /// </summary>
    private void UseDarkTitleBarWhenSystemIsDark()
    {
        if (!Brand.IsDark)
        {
            return;
        }

        try
        {
            var dark = 1;
            _ = NativeDwm.DwmSetWindowAttribute(
                Handle, NativeDwm.DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older Windows: the light caption is cosmetic, not a failure.
        }
    }

    /// <summary>
    /// WinForms controls do not follow the system theme, so the brand surface and text roles are
    /// pushed down the tree explicitly. Inputs and lists need it most: left alone they stay white
    /// on a navy form.
    /// </summary>
    private static void ApplyBrandColours(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case TabPage page:
                    page.BackColor = Brand.SurfaceBase;
                    page.ForeColor = Brand.TextPrimary;
                    break;

                case TextBox textBox:
                    textBox.BackColor = textBox.ReadOnly ? Brand.SurfaceRaised : Brand.SurfaceOverlay;
                    textBox.ForeColor = Brand.TextPrimary;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ListView list:
                    list.BackColor = Brand.SurfaceOverlay;
                    list.ForeColor = Brand.TextPrimary;
                    break;

                case NumericUpDown numeric:
                    numeric.BackColor = Brand.SurfaceOverlay;
                    numeric.ForeColor = Brand.TextPrimary;
                    break;

                case LinkLabel link:
                    link.LinkColor = Brand.AccentInk;
                    link.ActiveLinkColor = Brand.AccentInteractive;
                    link.BackColor = Color.Transparent;
                    break;

                case Label label:
                    // Grey hints keep their muted role; everything else takes primary text.
                    label.ForeColor = label.ForeColor == SystemColors.GrayText
                        ? Brand.TextSecondary
                        : Brand.TextPrimary;
                    label.BackColor = Color.Transparent;
                    break;

                case CheckBox check:
                    check.ForeColor = Brand.TextPrimary;
                    check.BackColor = Color.Transparent;
                    break;

                case Button button when button.FlatStyle != FlatStyle.Flat:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Brand.SurfaceRaised;
                    button.ForeColor = Brand.TextPrimary;
                    button.FlatAppearance.BorderColor = Brand.BorderSubtle;
                    break;
            }

            if (control.HasChildren)
            {
                ApplyBrandColours(control);
            }
        }
    }

    internal event EventHandler<AppSettings>? SettingsChanged;

    internal void NotifyDisplaysChanged(IReadOnlyList<DisplayTarget> displays)
    {
        _displays = displays;
        if (!IsDisposed && IsHandleCreated)
        {
            BeginInvoke(RefreshDiagnostics);
        }
    }

    // ---------------------------------------------------------------- tabs

    private TabPage BuildGeneralTab()
    {
        var page = NewPage("General");
        var y = 16;

        _startWithWindows.Text = "Start HDR Switch when I sign in";
        Place(page, _startWithWindows, ref y);

        _showNoticeOnToggle.Text = "Show a brief confirmation when HDR changes";
        Place(page, _showNoticeOnToggle, ref y);

        y += 10;
        _hotkeyEnabled.Text = "Global hotkey";
        Place(page, _hotkeyEnabled, ref y);

        _hotkeyText.SetBounds(34, y, 180, 24);
        _hotkeyText.TextChanged += (_, _) => ValidateHotkey();
        page.Controls.Add(_hotkeyText);

        var hint = new Label
        {
            Text = "e.g. Ctrl+Alt+H, Win+Shift+F9",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(224, y + 4),
        };
        page.Controls.Add(hint);
        y += 30;

        _hotkeyStatus.SetBounds(34, y, 540, 34);
        _hotkeyStatus.ForeColor = SystemColors.GrayText;
        page.Controls.Add(_hotkeyStatus);
        y += 42;

        var cliHeader = new Label
        {
            Text = "Command line (for desktop shortcuts, Stream Deck, AutoHotkey)",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(16, y),
        };
        page.Controls.Add(cliHeader);
        y += 24;

        var cli = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine,
            [
                "HdrSwitch.exe toggle            flip every HDR-capable display",
                "HdrSwitch.exe on --display 2    turn display 2 on",
                "HdrSwitch.exe off --display Samsung",
                "HdrSwitch.exe status --json     machine-readable state",
                "HdrSwitch.exe selftest          diagnose the display API",
            ]),
        };
        cli.SetBounds(16, y, 570, 96);
        cli.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(cli);

        return page;
    }

    private TabPage BuildSharingTab()
    {
        var page = NewPage("Screen sharing");
        var y = 16;

        var blurb = new Label
        {
            Text = "Windows records which apps capture the screen. When one starts while HDR is on, " +
                   "HDR Switch can offer to turn HDR off so viewers do not see washed-out colour.",
            AutoSize = false,
            Location = new Point(16, y),
            Size = new Size(570, 36),
            ForeColor = SystemColors.GrayText,
        };
        page.Controls.Add(blurb);
        y += 44;

        _watchSharing.Text = "Watch for screen sharing";
        Place(page, _watchSharing, ref y);

        _restoreAfter.Text = "Offer to restore HDR when sharing ends";
        Place(page, _restoreAfter, ref y);

        var secondsLabel = new Label { Text = "Dismiss the prompt after", AutoSize = true, Location = new Point(16, y + 4) };
        page.Controls.Add(secondsLabel);
        _toastSeconds.SetBounds(160, y, 60, 24);
        _toastSeconds.Minimum = 5;
        _toastSeconds.Maximum = 120;
        page.Controls.Add(_toastSeconds);
        page.Controls.Add(new Label { Text = "seconds", AutoSize = true, Location = new Point(226, y + 4) });
        y += 36;

        var rulesHeader = new Label
        {
            Text = "What HDR Switch has learned",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(16, y),
        };
        page.Controls.Add(rulesHeader);
        y += 24;

        _rulesList.View = View.Details;
        _rulesList.FullRowSelect = true;
        _rulesList.MultiSelect = false;
        _rulesList.Dock = DockStyle.Fill;
        _rulesList.Columns.Add("App", 200);
        _rulesList.Columns.Add("When it shares my screen", 240);
        _rulesList.Columns.Add("Answers", 110);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
        };

        actions.Controls.Add(MakeRuleButton("Ask me", RuleState.Ask));
        actions.Controls.Add(MakeRuleButton("Always turn HDR off", RuleState.AutoTurnOff));
        actions.Controls.Add(MakeRuleButton("Never ask", RuleState.AutoKeep));

        var remove = new Button { Text = "Forget", Width = 80, Height = 26 };
        remove.Click += (_, _) =>
        {
            if (SelectedRuleKey() is { } key)
            {
                _rules.Remove(key);
                RefreshRules();
            }
        };
        actions.Controls.Add(remove);

        // A TableLayoutPanel rather than anchors: a bottom-anchored button row and a
        // bottom-anchored list fight over the same pixels, and the list wins -- which silently
        // hid the buttons that make the learned rules editable at all.
        page.Controls.Add(StackListOverActions(_rulesList, actions, new Point(16, y)));

        return page;
    }

    private TabPage BuildGamesTab()
    {
        var page = NewPage("Games");
        var y = 16;

        var blurb = new Label
        {
            Text = "Turn HDR on automatically while a game is running, and put it back when the game exits.",
            AutoSize = false,
            Location = new Point(16, y),
            Size = new Size(570, 20),
            ForeColor = SystemColors.GrayText,
        };
        page.Controls.Add(blurb);
        y += 28;

        _watchGames.Text = "Watch for games";
        Place(page, _watchGames, ref y);

        _gamesList.View = View.Details;
        _gamesList.FullRowSelect = true;
        _gamesList.CheckBoxes = true;
        _gamesList.Dock = DockStyle.Fill;
        _gamesList.Columns.Add("Executable", 220);
        _gamesList.Columns.Add("Name", 200);
        _gamesList.Columns.Add("Displays", 130);
        _gamesList.ItemChecked += (_, e) =>
        {
            if (e.Item.Tag is GameRule rule)
            {
                rule.Enabled = e.Item.Checked;
            }
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = Padding.Empty,
        };

        var add = new Button { Text = "Add game…", Width = 100, Height = 26 };
        add.Click += (_, _) => AddGameRule();
        actions.Controls.Add(add);

        var remove = new Button { Text = "Remove", Width = 90, Height = 26 };
        remove.Click += (_, _) =>
        {
            if (_gamesList.SelectedItems.Count > 0 && _gamesList.SelectedItems[0].Tag is GameRule rule)
            {
                _working.GameRules.Remove(rule);
                RefreshGames();
            }
        };
        actions.Controls.Add(remove);

        page.Controls.Add(StackListOverActions(_gamesList, actions, new Point(16, y)));
        return page;
    }

    /// <summary>
    /// Puts a list above a fixed-height action row, both growing with the window. Explicit rows
    /// avoid the anchor conflict that otherwise pushes the buttons off the page.
    /// </summary>
    private TableLayoutPanel StackListOverActions(Control list, Control actions, Point origin)
    {
        var layout = new TableLayoutPanel
        {
            Location = origin,
            Size = new Size(570, 300),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        _stackedPanels.Add(layout);
        return layout;
    }

    /// <summary>
    /// A fixed design-time height plus bottom anchoring is not enough: the tab page is shorter
    /// than the panel asks for, so the action row lands below the visible area and the buttons
    /// that edit the learned rules simply are not there. Size them once the page is real.
    /// </summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitStackedPanels();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        FitStackedPanels();
    }

    private void FitStackedPanels()
    {
        foreach (var panel in _stackedPanels)
        {
            if (panel.Parent is not { } parent)
            {
                continue;
            }

            var available = parent.ClientSize.Height - panel.Top - 12;
            if (available > 80)
            {
                panel.Height = available;
            }

            panel.Width = Math.Max(200, parent.ClientSize.Width - panel.Left - 16);
        }
    }

    private TabPage BuildAdvancedTab()
    {
        var page = NewPage("Advanced");
        var y = 16;

        _heuristicEnabled.Text = "Also guess from running processes (approximate)";
        Place(page, _heuristicEnabled, ref y);

        var blurb = new Label
        {
            Text = "Windows only records apps that capture through the modern API — which on Windows 11 is " +
                   "essentially all of them. Older capture tools do not appear at all. This fallback simply " +
                   "checks whether an executable is running, so it cannot tell \"open\" from \"sharing\" and " +
                   "will produce false alarms. One executable per line.",
            AutoSize = false,
            Location = new Point(34, y),
            Size = new Size(552, 78),
            ForeColor = SystemColors.GrayText,
        };
        page.Controls.Add(blurb);
        y += 84;

        _heuristicList.SetBounds(34, y, 552, 90);
        _heuristicList.Multiline = true;
        _heuristicList.ScrollBars = ScrollBars.Vertical;
        _heuristicList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(_heuristicList);
        y += 96;

        var suggest = new LinkLabel
        {
            Text = "Insert common capture tools",
            AutoSize = true,
            Location = new Point(34, y),
        };
        suggest.LinkClicked += (_, _) =>
        {
            var existing = _heuristicList.Lines.Where(l => l.Trim().Length > 0).ToList();
            foreach (var candidate in ProcessHeuristic.SuggestedWatchList)
            {
                if (!existing.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    existing.Add(candidate);
                }
            }

            _heuristicList.Lines = existing.ToArray();
        };
        page.Controls.Add(suggest);
        y += 32;

        var diagHeader = new Label
        {
            Text = "Diagnostics",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(16, y),
        };
        page.Controls.Add(diagHeader);
        y += 24;

        _diagnostics.SetBounds(16, y, 570, 120);
        _diagnostics.ForeColor = SystemColors.GrayText;
        _diagnostics.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(_diagnostics);

        return page;
    }

    // ---------------------------------------------------------------- helpers

    private static TabPage NewPage(string title) => new(title)
    {
        BackColor = SystemColors.Window,
        Padding = new Padding(8),
        AutoScroll = true,
    };

    private static void Place(Control parent, Control control, ref int y)
    {
        control.SetBounds(16, y, 540, 24);
        parent.Controls.Add(control);
        y += 30;
    }

    private Button MakeRuleButton(string text, RuleState state)
    {
        var button = new Button { Text = text, Width = 150, Height = 26, AutoSize = true };
        button.Click += (_, _) =>
        {
            if (SelectedRuleKey() is not { } key)
            {
                return;
            }

            var rule = _rules.Find(key);
            _rules.SetState(key, rule?.DisplayName ?? key, state);
            RefreshRules();
        };
        return button;
    }

    private string? SelectedRuleKey() =>
        _rulesList.SelectedItems.Count > 0 && _rulesList.SelectedItems[0].Tag is AppRule rule
            ? rule.AppKey
            : null;

    private void LoadValues()
    {
        _startWithWindows.Checked = StartupRegistration.IsEnabled();
        _showNoticeOnToggle.Checked = _working.ShowBalloonOnToggle;
        _hotkeyEnabled.Checked = _working.HotkeyEnabled;
        _hotkeyText.Text = _working.Hotkey;

        _watchSharing.Checked = _working.WatchScreenSharing;
        _restoreAfter.Checked = _working.RestoreHdrAfterSharing;
        _toastSeconds.Value = Math.Clamp(_working.ToastSeconds, (int)_toastSeconds.Minimum, (int)_toastSeconds.Maximum);

        _watchGames.Checked = _working.WatchGames;
        _heuristicEnabled.Checked = _working.ProcessHeuristicEnabled;
        _heuristicList.Lines = _working.ProcessWatchList.ToArray();

        RefreshRules();
        RefreshGames();
        ValidateHotkey();
        RefreshDiagnostics();
    }

    private void RefreshRules()
    {
        _rulesList.BeginUpdate();
        _rulesList.Items.Clear();

        foreach (var rule in _rules.Rules.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var behaviour = rule.State switch
            {
                RuleState.AutoTurnOff => "Turn HDR off automatically",
                RuleState.AutoKeep => "Leave HDR alone, stay quiet",
                _ => "Ask me",
            };

            var item = new ListViewItem(rule.DisplayName is { Length: > 0 } ? rule.DisplayName : rule.AppKey)
            {
                Tag = rule,
            };
            item.SubItems.Add(behaviour);
            item.SubItems.Add($"off {rule.TurnOffCount} / keep {rule.KeepCount}");
            _rulesList.Items.Add(item);
        }

        if (_rulesList.Items.Count == 0)
        {
            _rulesList.Items.Add(new ListViewItem("Nothing learned yet")
            {
                ForeColor = SystemColors.GrayText,
            });
        }

        _rulesList.EndUpdate();
    }

    private void RefreshGames()
    {
        _gamesList.BeginUpdate();
        _gamesList.Items.Clear();

        foreach (var rule in _working.GameRules)
        {
            var item = new ListViewItem(rule.ExeName) { Tag = rule, Checked = rule.Enabled };
            item.SubItems.Add(rule.DisplayName);
            item.SubItems.Add(rule.DisplayIds.Count == 0 ? "All capable" : $"{rule.DisplayIds.Count} selected");
            _gamesList.Items.Add(item);
        }

        _gamesList.EndUpdate();
    }

    private void RefreshDiagnostics()
    {
        var controller = new HdrController();
        var api = controller.ApiPath == HdrApiPath.Unknown ? "resolved on first use" : controller.ApiPath.ToString();

        var lines = new List<string>
        {
            $"Windows: {Environment.OSVersion.VersionString}",
            $"Display API path: {api}",
            $"Settings file: {SettingsStore.DefaultPath}",
            $"Displays detected: {_displays.Count} " +
            $"({_displays.Count(d => d.CanToggle)} HDR-capable)",
        };

        foreach (var display in _displays)
        {
            lines.Add($"    {display.Label}: {display.StatusText}");
        }

        _diagnostics.Text = string.Join(Environment.NewLine, lines);
    }

    private void ValidateHotkey()
    {
        if (!_hotkeyEnabled.Checked)
        {
            _hotkeyStatus.Text = "The hotkey is off. HDR Switch still responds to the tray icon and the command line.";
            return;
        }

        _hotkeyStatus.Text = HotkeyParser.TryParse(_hotkeyText.Text, out var parsed, out var error) && parsed is not null
            ? $"Will register {parsed.Text}."
            : error ?? "That hotkey cannot be used.";
    }

    private void AddGameRule()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Pick the game executable",
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var exeName = ProcessHeuristic.NormalizeExeName(dialog.FileName);
        if (_working.GameRules.Any(g => string.Equals(g.ExeName, exeName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"{exeName} is already in the list.", "HDR Switch",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _working.GameRules.Add(new GameRule
        {
            ExeName = exeName,
            DisplayName = Path.GetFileNameWithoutExtension(dialog.FileName),
            Enabled = true,
            DisplayIds = [],
        });

        RefreshGames();
    }

    private void Apply()
    {
        if (_hotkeyEnabled.Checked &&
            !HotkeyParser.TryParse(_hotkeyText.Text, out _, out var hotkeyError))
        {
            MessageBox.Show(this, hotkeyError, "HDR Switch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (StartupRegistration.IsEnabled() != _startWithWindows.Checked || StartupRegistration.IsStale())
            {
                StartupRegistration.SetEnabled(_startWithWindows.Checked);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not change the startup entry: {ex.Message}", "HDR Switch",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        _working.ShowBalloonOnToggle = _showNoticeOnToggle.Checked;
        _working.HotkeyEnabled = _hotkeyEnabled.Checked;
        _working.Hotkey = _hotkeyText.Text.Trim();

        _working.WatchScreenSharing = _watchSharing.Checked;
        _working.RestoreHdrAfterSharing = _restoreAfter.Checked;
        _working.ToastSeconds = (int)_toastSeconds.Value;

        _working.WatchGames = _watchGames.Checked;
        _working.ProcessHeuristicEnabled = _heuristicEnabled.Checked;
        _working.ProcessWatchList = _heuristicList.Lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SettingsChanged?.Invoke(this, _working.Clone());
        Close();
    }
}
