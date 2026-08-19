using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using HdrSwitch.Core.Config;
using HdrSwitch.Core.Rules;

namespace HdrSwitch.Ui;

/// <summary>
/// Renders the vendored brand assets to a PNG so they can actually be looked at.
///
/// The wordmark is reconstructed from SVG Bezier data by a hand-written parser; a mistake there
/// would produce a plausible-looking but wrong shape that no unit test would catch. This exists
/// so the mark, the palette and the tray icons can be reviewed by eye after a re-sync from
/// design-system-kit.
/// </summary>
internal static class BrandPreview
{
    internal static string Render(string? path)
    {
        var target = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(Path.GetTempPath(), "hdrswitch-brandcheck.png")
            : path;

        // Creating the directory is friendlier than failing on a path the caller clearly meant.
        var parent = Path.GetDirectoryName(Path.GetFullPath(target));
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        const int width = 760;
        const int height = 470;

        using var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var light = Color.White;
            var navy = Brand.Navy;

            // Both sanctioned pairings side by side: navy on light, white on navy.
            g.FillRectangle(new SolidBrush(light), 0, 0, width, height / 2);
            g.FillRectangle(new SolidBrush(navy), 0, height / 2, width, height / 2);

            Wordmark.Draw(g, new Point(24, 26), 200, Wordmark.ColourFor(light));
            Wordmark.Draw(g, new Point(24, (height / 2) + 26), 200, Wordmark.ColourFor(navy));

            using var labelFont = Brand.Body(8.5f);
            g.DrawString("navy on light — 18.44:1", labelFont, new SolidBrush(Brand.Navy), 24, 150);
            g.DrawString("white on navy — 18.44:1", labelFont, new SolidBrush(Color.White), 24, (height / 2) + 150);

            // Minimum-size guard: this one must refuse to draw.
            var tooSmall = Wordmark.Draw(g, new Point(300, 40), Wordmark.MinimumWidthPx - 20, Brand.Navy);
            g.DrawString(
                tooSmall ? "BUG: drew below the 80px minimum" : "below 80px: correctly not drawn",
                labelFont, new SolidBrush(tooSmall ? Brand.StateDanger : Brand.TextSecondary), 300, 40);

            Wordmark.Draw(g, new Point(300, 66), Wordmark.MinimumWidthPx, Brand.Navy);
            g.DrawString("at the 80px minimum", labelFont, new SolidBrush(Brand.TextSecondary), 300, 104);

            // Tray icons at real sizes.
            var icons = new[] { ("on", IconFactory.On), ("off", IconFactory.Off), ("blocked", IconFactory.Unavailable) };
            var x = 300;
            foreach (var (name, icon) in icons)
            {
                g.DrawIcon(icon, new Rectangle(x, 140, 32, 32));
                g.DrawIcon(icon, new Rectangle(x + 40, 148, 16, 16));
                g.DrawString(name, labelFont, new SolidBrush(Brand.TextSecondary), x, 176);
                x += 90;
            }

            x = 300;
            foreach (var (name, icon) in icons)
            {
                g.DrawIcon(icon, new Rectangle(x, (height / 2) + 140, 32, 32));
                g.DrawIcon(icon, new Rectangle(x + 40, (height / 2) + 148, 16, 16));
                g.DrawString(name, labelFont, new SolidBrush(Color.White), x, (height / 2) + 176);
                x += 90;
            }

            // The sanctioned brand gradient, and the palette swatches.
            var gradientRect = new Rectangle(24, 190, 250, 26);
            // The kit specifies 135deg in CSS terms (towards bottom-right). GDI+ measures its
            // angle from the positive x-axis with y pointing down, so the same direction is 45f.
            // Passing 135f here renders the gradient reversed.
            using (var gradient = new LinearGradientBrush(gradientRect, Brand.Navy, Brand.Cyan, 45f))
            {
                gradient.InterpolationColors = new ColorBlend
                {
                    Colors = Brand.BrandGradient,
                    Positions = [0f, 0.5f, 1f],
                };
                g.FillRectangle(gradient, gradientRect);
            }

            var swatches = new (string Name, Color Colour)[]
            {
                ("navy", Brand.Navy), ("blue", Brand.DeepBlue), ("cyan", Brand.Cyan),
                ("teal", Brand.Teal), ("grey", Brand.LightGrey),
            };
            var sx = 24;
            foreach (var (name, colour) in swatches)
            {
                g.FillRectangle(new SolidBrush(colour), sx, (height / 2) + 190, 44, 26);
                g.DrawRectangle(new Pen(Brand.BorderStrong), sx, (height / 2) + 190, 44, 26);
                g.DrawString(name, labelFont, new SolidBrush(Color.White), sx, (height / 2) + 218);
                sx += 50;
            }
        }

        bitmap.Save(target, ImageFormat.Png);
        return target;
    }

    /// <summary>
    /// Renders every Settings tab to a PNG in-process.
    ///
    /// Driving the real window with synthetic clicks proved unreliable, and the thing worth
    /// checking is simply whether every control lands inside the visible client area -- an
    /// earlier layout put the rule-editing buttons below the bottom of the page, where they were
    /// invisible and the learned rules could not be changed at all.
    /// </summary>
    internal static IReadOnlyList<string> RenderSettingsTabs(string directory)
    {
        Directory.CreateDirectory(directory);
        var written = new List<string>();

        var settings = new AppSettings();
        settings.AppRules.Add(new AppRule
        {
            AppKey = "discord.exe",
            DisplayName = "Discord",
            State = RuleState.AutoTurnOff,
            TurnOffCount = 2,
        });
        settings.GameRules.Add(new GameRule
        {
            ExeName = "cyberpunk2077.exe",
            DisplayName = "Cyberpunk 2077",
            Enabled = true,
        });

        using var form = new SettingsForm(settings, new RuleEngine(settings.AppRules), []);

        // Off-screen rather than hidden: DrawToBitmap needs a realised, laid-out window.
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-4000, -4000);
        form.ShowInTaskbar = false;
        form.Show();

        var tabs = FindTabControl(form);
        var count = tabs?.TabPages.Count ?? 1;

        for (var i = 0; i < count; i++)
        {
            if (tabs is not null)
            {
                tabs.SelectedIndex = i;
            }

            form.Refresh();
            Application.DoEvents();

            using var shot = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(shot, new Rectangle(0, 0, form.Width, form.Height));

            var name = tabs?.TabPages[i].Text.Replace(' ', '-').ToLowerInvariant() ?? "settings";
            var file = Path.Combine(directory, $"settings-{i}-{name}.png");
            shot.Save(file, ImageFormat.Png);
            written.Add(file);
        }

        form.Close();
        return written;
    }

    /// <summary>
    /// Renders the screen-sharing prompt and the learned-action notice. This is the surface the
    /// whole feature is judged on and it only ever appears at an awkward moment, so being able
    /// to look at it on demand matters.
    /// </summary>
    internal static IReadOnlyList<string> RenderToasts(string directory)
    {
        Directory.CreateDirectory(directory);
        var written = new List<string>();

        var suggestion = ToastWindow.ShowSuggestion(
            "Discord",
            "Discord is capturing your screen",
            "HDR is on for LS27AG55x. Captured HDR usually reaches viewers washed out and "
            + "desaturated, because it gets flattened to SDR on the way.",
            60,
            _ => { });

        var notice = ToastWindow.ShowNotice(
            "HDR off — Discord is sharing",
            "HDR Switch did this automatically because that is what you chose before.",
            60,
            "Undo and ask me next time",
            () => { });

        foreach (var (toast, name) in new[] { (suggestion, "suggestion"), (notice, "notice") })
        {
            toast.Opacity = 1;
            toast.Refresh();
            Application.DoEvents();

            using var shot = new Bitmap(toast.Width, toast.Height);
            toast.DrawToBitmap(shot, new Rectangle(0, 0, toast.Width, toast.Height));

            var file = Path.Combine(directory, $"toast-{name}.png");
            shot.Save(file, ImageFormat.Png);
            written.Add(file);

            toast.Dismiss();
        }

        return written;
    }

    private static TabControl? FindTabControl(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is TabControl tabs)
            {
                return tabs;
            }

            var nested = FindTabControl(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
