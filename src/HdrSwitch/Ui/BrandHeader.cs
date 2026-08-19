using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace HdrSwitch.Ui;

internal static class NativeDwm
{
    internal const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", SetLastError = true)]
    internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

/// <summary>
/// The settings window header: the preunec wordmark, the product name, and a hairline rule.
///
/// The wordmark is drawn from the design kit's outlined SVG (see <see cref="Wordmark"/>), in one
/// flat colour, at a size above the 80 px minimum, with its full 1x cap-height clear space kept
/// free. If the asset cannot be loaded the header degrades to the product name alone rather than
/// substituting a typeface for the mark.
/// </summary>
internal sealed class BrandHeader : Panel
{
    private const int MarkWidth = 116;
    private const int PaddingX = 16;

    internal BrandHeader()
    {
        DoubleBuffered = true;
        BackColor = Brand.SurfaceRaised;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var markColour = Wordmark.ColourFor(BackColor);
        var block = Wordmark.MeasureWithClearSpace(MarkWidth);
        var top = Math.Max(0, (Height - block.Height) / 2);

        var drawn = Wordmark.Draw(graphics, new Point(PaddingX, top), MarkWidth, markColour);

        // The clear-space box is part of the mark's footprint: nothing may enter it, so the
        // divider and product name start after the full block, not after the glyph.
        var x = drawn ? PaddingX + block.Width : PaddingX;

        if (drawn)
        {
            using var rule = new Pen(Brand.BorderStrong, 1f);
            graphics.DrawLine(rule, x, (Height / 2) - 12, x, (Height / 2) + 12);
            x += 14;
        }

        using var font = Brand.Display(12f);
        using var brush = new SolidBrush(Brand.TextPrimary);
        var size = graphics.MeasureString("HDR Switch", font);
        graphics.DrawString("HDR Switch", font, brush, x, (Height - size.Height) / 2f);

        using var border = new Pen(Brand.BorderSubtle, 1f);
        graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
    }
}

/// <summary>
/// A TabControl whose strip background follows the brand surface.
///
/// Owner-drawing only covers the tab items themselves; the native control still erases the
/// leftover strip to the right of the last tab with a system colour, which leaves a light band
/// across a dark window. Intercepting WM_ERASEBKGND is the least invasive fix that does not
/// disturb the control's own tab rendering.
/// </summary>
internal sealed class BrandTabControl : TabControl
{
    private const int WM_ERASEBKGND = 0x0014;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ERASEBKGND && m.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(m.WParam);
            using var brush = new SolidBrush(Brand.SurfaceRaised);
            graphics.FillRectangle(brush, ClientRectangle);
            m.Result = 1;
            return;
        }

        base.WndProc(ref m);
    }
}
