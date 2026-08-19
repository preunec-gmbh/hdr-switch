using System.Drawing.Drawing2D;
using HdrSwitch.Core.Rules;

namespace HdrSwitch.Ui;

/// <summary>
/// A small notification window anchored above the tray.
///
/// Critically it never takes focus: WS_EX_NOACTIVATE plus ShowWithoutActivation. This appears at
/// the exact moment the user is starting a screen share or a presentation, and stealing focus
/// then would be worse than not warning at all.
/// </summary>
internal sealed class ToastWindow : Form
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;

    private const int EdgeMargin = 12;
    private const int Inset = 16;

    private static readonly List<ToastWindow> Open = [];

    private readonly System.Windows.Forms.Timer _dismissTimer = new();
    private readonly System.Windows.Forms.Timer _fadeTimer = new();
    private Action? _onDismissed;
    private bool _answered;

    private ToastWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Brand.SurfaceOverlay;
        Opacity = 0;
        DoubleBuffered = true;
        Font = Brand.Body(9f);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    /// <summary>
    /// The "an app started sharing your screen" prompt, with the three answers the rule engine
    /// understands.
    /// </summary>
    internal static ToastWindow ShowSuggestion(
        string appName,
        string headline,
        string detail,
        int seconds,
        Action<CaptureAnswer> onAnswer)
    {
        var toast = new ToastWindow();
        var answered = false;

        void Answer(CaptureAnswer answer)
        {
            if (answered)
            {
                return;
            }

            answered = true;
            toast._answered = true;
            onAnswer(answer);
            toast.Dismiss();
        }

        var y = toast.BuildHeader(headline, detail);

        var turnOff = toast.MakeButton("Turn HDR off", primary: true);
        var keep = toast.MakeButton("Keep HDR", primary: false);
        turnOff.Click += (_, _) => Answer(CaptureAnswer.TurnOff);
        keep.Click += (_, _) => Answer(CaptureAnswer.Keep);

        turnOff.Location = new Point(Inset, y);
        keep.Location = new Point(Inset + turnOff.Width + 8, y);
        toast.Controls.Add(turnOff);
        toast.Controls.Add(keep);
        y += turnOff.Height + 10;

        var never = new LinkLabel
        {
            Text = $"Never ask for {appName}",
            AutoSize = true,
            Location = new Point(Inset, y),
            LinkColor = Brand.TextSecondary,
            ActiveLinkColor = Brand.AccentInteractive,
            BackColor = Color.Transparent,
            LinkBehavior = LinkBehavior.HoverUnderline,
        };
        never.LinkClicked += (_, _) => Answer(CaptureAnswer.NeverAsk);
        toast.Controls.Add(never);
        y += never.PreferredHeight + Inset;

        toast.ClientSize = new Size(400, y);

        // Timing out is not an answer -- the rule stays in "Ask" and nothing changes.
        toast.Launch(seconds, onDismissed: null);
        return toast;
    }

    /// <summary>A passive notice, optionally with a single action such as Undo.</summary>
    internal static ToastWindow ShowNotice(
        string headline,
        string detail,
        int seconds,
        string? actionText = null,
        Action? onAction = null)
    {
        var toast = new ToastWindow();
        var y = toast.BuildHeader(headline, detail);

        if (actionText is { Length: > 0 } && onAction is not null)
        {
            var action = toast.MakeButton(actionText, primary: false);
            action.Location = new Point(Inset, y);
            action.Click += (_, _) =>
            {
                toast._answered = true;
                onAction();
                toast.Dismiss();
            };
            toast.Controls.Add(action);
            y += action.Height + Inset;
        }
        else
        {
            y += 4;
        }

        toast.ClientSize = new Size(400, y);
        toast.Launch(seconds, onDismissed: null);
        return toast;
    }

    private int BuildHeader(string headline, string detail)
    {
        var y = Inset;

        var title = new Label
        {
            Text = headline,
            Font = Brand.Display(10.5f),
            ForeColor = Brand.TextPrimary,
            BackColor = Color.Transparent,
            Location = new Point(Inset, y),
            Size = new Size(400 - (Inset * 2) - 24, 0),
            AutoSize = false,
        };
        title.Height = TextRenderer.MeasureText(headline, title.Font, new Size(title.Width, 0),
            TextFormatFlags.WordBreak).Height + 2;
        Controls.Add(title);
        y += title.Height + 6;

        var body = new Label
        {
            Text = detail,
            ForeColor = Brand.TextSecondary,
            BackColor = Color.Transparent,
            Location = new Point(Inset, y),
            Size = new Size(400 - (Inset * 2), 0),
            AutoSize = false,
        };
        body.Height = TextRenderer.MeasureText(detail, body.Font, new Size(body.Width, 0),
            TextFormatFlags.WordBreak).Height + 4;
        Controls.Add(body);
        y += body.Height + 14;

        var close = new Label
        {
            Text = "✕",
            AutoSize = true,
            ForeColor = Brand.TextSecondary,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Location = new Point(400 - Inset - 14, Inset),
        };
        close.Click += (_, _) => Dismiss();
        Controls.Add(close);
        close.BringToFront();

        return y;
    }

    private Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = false,
            FlatStyle = FlatStyle.Flat,
            Height = 30,
            BackColor = primary ? Brand.AccentInteractive : Brand.SurfaceRaised,
            ForeColor = primary ? Brand.AccentInteractiveFg : Brand.TextPrimary,
            Cursor = Cursors.Hand,
            // Buttons in a non-activating window should not paint a focus rectangle.
            TabStop = false,
        };
        button.FlatAppearance.BorderColor = primary ? Brand.AccentInteractive : Brand.BorderSubtle;
        button.FlatAppearance.BorderSize = 1;
        button.Width = TextRenderer.MeasureText(text, button.Font).Width + 28;
        return button;
    }

    private void Launch(int seconds, Action? onDismissed)
    {
        _onDismissed = onDismissed;

        PositionAboveTray();

        _fadeTimer.Interval = 15;
        _fadeTimer.Tick += (_, _) =>
        {
            if (Opacity >= 1)
            {
                _fadeTimer.Stop();
                return;
            }

            Opacity = Math.Min(1, Opacity + 0.12);
        };

        _dismissTimer.Interval = Math.Max(3, seconds) * 1000;
        _dismissTimer.Tick += (_, _) => Dismiss();

        lock (Open)
        {
            Open.Add(this);
        }

        Show();
        _fadeTimer.Start();
        _dismissTimer.Start();
    }

    private void PositionAboveTray()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);

        var offset = 0;
        lock (Open)
        {
            foreach (var other in Open)
            {
                offset += other.Height + 8;
            }
        }

        Location = new Point(
            area.Right - Width - EdgeMargin,
            Math.Max(area.Top + EdgeMargin, area.Bottom - Height - EdgeMargin - offset));
    }

    /// <summary>True when the user actually chose something rather than letting it lapse.</summary>
    internal bool WasAnswered => _answered;

    internal void Dismiss()
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        _dismissTimer.Stop();
        _fadeTimer.Stop();
        _onDismissed?.Invoke();
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        lock (Open)
        {
            Open.Remove(this);
        }

        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Brand.BorderSubtle, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        // The sanctioned "brand" gradient (navy -> blue -> cyan). It is a background element
        // only; no text sits on it, which is the condition the brand guidelines attach to it.
        var stripe = new Rectangle(0, 0, 4, Height);
        var colours = Brand.AccentStripeGradient;
        using var accent = new LinearGradientBrush(stripe, colours[0], colours[^1], LinearGradientMode.Vertical)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = colours,
                Positions = [0f, 0.5f, 1f],
            },
        };
        e.Graphics.FillRectangle(accent, stripe);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dismissTimer.Dispose();
            _fadeTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
