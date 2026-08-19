using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace HdrSwitch.Ui;

/// <summary>
/// Tray icons drawn at runtime, so the single-file executable carries no binary assets.
///
/// The glyph is a split disc: a dark half against a bright half, which is literally what dynamic
/// range means. When HDR is on the two halves are at full contrast; when off they collapse to a
/// flat mid grey. That difference survives being scaled to 16x16, which a lettered icon would not.
/// </summary>
internal static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly Dictionary<string, Icon> Cache = [];
    private static readonly List<IntPtr> OwnedHandles = [];

    /// <summary>
    /// The product mark, for the executable, taskbar and window icons.
    ///
    /// Deliberately NOT used in the tray: the tray icon's job is to show whether HDR is on, and
    /// three letters do not survive being scaled to 16x16. Identity goes on the window, state
    /// goes in the tray.
    /// </summary>
    internal static Icon App
    {
        get
        {
            lock (Cache)
            {
                if (Cache.TryGetValue("app", out var cached))
                {
                    return cached;
                }

                using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("HdrSwitch.Brand.app.ico");

                var icon = stream is not null ? new Icon(stream) : On;
                Cache["app"] = icon;
                return icon;
            }
        }
    }

    internal static Icon On => Get("on", static g => Draw(g, enabled: true, blocked: false));

    internal static Icon Off => Get("off", static g => Draw(g, enabled: false, blocked: false));

    internal static Icon Unavailable => Get("blocked", static g => Draw(g, enabled: false, blocked: true));

    internal static Icon ForState(bool anyEnabled, bool anyCapable) =>
        !anyCapable ? Unavailable : anyEnabled ? On : Off;

    private static Icon Get(string key, Action<Graphics> draw)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // 32x32 gives Windows a clean downscale to whatever the tray actually asks for.
            using var bitmap = new Bitmap(32, 32);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Transparent);
                draw(graphics);
            }

            var handle = bitmap.GetHicon();
            OwnedHandles.Add(handle);

            // Clone so the Icon survives DestroyIcon at shutdown in the right order.
            using var temp = Icon.FromHandle(handle);
            var icon = (Icon)temp.Clone();

            Cache[key] = icon;
            return icon;
        }
    }

    private static void Draw(Graphics graphics, bool enabled, bool blocked)
    {
        var bounds = new Rectangle(2, 2, 28, 28);

        // Brand roles, not arbitrary colours: navy is structure, cyan is technology and
        // interactivity, muted grey is the inactive neutral. Cyan is legal here because this is
        // a graphic fill, not text -- it would fail contrast as a label.
        var darkHalf = enabled ? Brand.Navy : Brand.TextMuted;
        var brightHalf = enabled ? Brand.LightGrey : Color.FromArgb(255, 156, 163, 196);
        var ring = enabled ? Brand.Cyan : Brand.BorderStrong;

        using (var path = new GraphicsPath())
        {
            path.AddPie(bounds, 90, 180);
            using var brush = new SolidBrush(darkHalf);
            graphics.FillPath(brush, path);
        }

        using (var path = new GraphicsPath())
        {
            path.AddPie(bounds, 270, 180);
            using Brush brush = enabled
                ? new LinearGradientBrush(bounds, brightHalf, Color.White, LinearGradientMode.Vertical)
                : new SolidBrush(brightHalf);
            graphics.FillPath(brush, path);
        }

        // The ring keeps the glyph legible against both a light and a dark taskbar, where the
        // navy half would otherwise disappear.
        using (var pen = new Pen(ring, 2f))
        {
            graphics.DrawEllipse(pen, bounds);
        }

        if (blocked)
        {
            using var pen = new Pen(Brand.StateDangerFill, 3f);
            graphics.DrawLine(pen, bounds.Left + 4, bounds.Bottom - 4, bounds.Right - 4, bounds.Top + 4);
        }
    }

    internal static void Dispose()
    {
        lock (Cache)
        {
            foreach (var icon in Cache.Values)
            {
                icon.Dispose();
            }

            Cache.Clear();

            foreach (var handle in OwnedHandles)
            {
                DestroyIcon(handle);
            }

            OwnedHandles.Clear();
        }
    }
}
