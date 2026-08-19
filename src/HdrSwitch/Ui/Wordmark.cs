using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HdrSwitch.Ui;

/// <summary>
/// Renders the preunec wordmark from the outlined SVG shipped by the design kit.
///
/// The brand guidelines are explicit that the mark must never be redrawn or re-typeset — the
/// supplied SVG is the only sanctioned source. Sabon Bold is commercially licensed and is not
/// distributed, so setting "preunec" in an installed font would be both a licensing problem and
/// a guideline violation. Instead the `mono` variant (fill="currentColor") is embedded verbatim
/// and its Bezier outline is converted to a GraphicsPath here.
///
/// Enforced from brand/guidelines.md:
///   * one flat colour, navy on light surfaces and white on dark (never two-tone, no effects)
///   * proportional scaling only
///   * clear space of 1x cap-height on all four sides
///   * minimum 80 px wide on screen — below that it is not drawn at all
/// </summary>
internal static partial class Wordmark
{
    private const string ResourceName = "HdrSwitch.Brand.preunec-wordmark-mono.svg";

    /// <summary>From the kit's wordmark metrics.json.</summary>
    private const float ViewBoxX = 1.2f;
    private const float ViewBoxY = -47.1f;
    private const float ViewBoxWidth = 397f;
    private const float ViewBoxHeight = 69.9f;

    /// <summary>1x cap-height, in the wordmark's own coordinate units.</summary>
    private const float ClearSpaceUnits = 71.2f;

    /// <summary>Below this the counters close up and it stops reading as a word.</summary>
    internal const int MinimumWidthPx = 80;

    internal const float AspectRatio = ViewBoxWidth / ViewBoxHeight;

    private static readonly Lazy<GraphicsPath?> Outline = new(LoadOutline);

    /// <summary>Total width including the mandatory clear space, for a given mark width.</summary>
    internal static Size MeasureWithClearSpace(int markWidth)
    {
        var scale = markWidth / ViewBoxWidth;
        var markHeight = ViewBoxHeight * scale;
        var margin = ClearSpaceUnits * scale;

        return new Size(
            (int)Math.Ceiling(markWidth + (margin * 2)),
            (int)Math.Ceiling(markHeight + (margin * 2)));
    }

    /// <summary>
    /// Draws the wordmark at <paramref name="markWidth"/>, positioning the mark inside its clear
    /// space starting at <paramref name="origin"/>. Returns false when it was not drawn.
    /// </summary>
    internal static bool Draw(Graphics graphics, Point origin, int markWidth, Color colour)
    {
        // Refusing to draw is the correct behaviour below the minimum size: a mark that does not
        // read as a word is worse than no mark.
        if (markWidth < MinimumWidthPx || Outline.Value is null)
        {
            return false;
        }

        var scale = markWidth / ViewBoxWidth;
        var margin = ClearSpaceUnits * scale;

        var previousSmoothing = graphics.SmoothingMode;
        var previousState = graphics.Save();

        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Proportional only: one uniform scale factor on both axes, never a stretch.
            graphics.TranslateTransform(origin.X + margin, origin.Y + margin);
            graphics.ScaleTransform(scale, scale);
            graphics.TranslateTransform(-ViewBoxX, -ViewBoxY);

            // One flat colour. No gradient, outline, shadow, or glow — all explicitly prohibited.
            using var brush = new SolidBrush(colour);
            graphics.FillPath(brush, Outline.Value);
            return true;
        }
        finally
        {
            graphics.Restore(previousState);
            graphics.SmoothingMode = previousSmoothing;
        }
    }

    /// <summary>Navy on light surfaces, white on dark. The only two sanctioned pairings here.</summary>
    internal static Color ColourFor(Color background) =>
        Luminance(background) > 0.5 ? Brand.Navy : Color.White;

    private static double Luminance(Color c)
    {
        static double Channel(int v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static GraphicsPath? LoadOutline()
    {
        try
        {
            var svg = ReadResource();
            if (svg is null)
            {
                return null;
            }

            var match = PathDataRegex().Match(svg);
            return match.Success ? BuildPath(match.Groups[1].Value) : null;
        }
        catch (Exception)
        {
            // A missing or malformed asset must not take the settings window down.
            return null;
        }
    }

    private static string? ReadResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Minimal SVG path reader. The outlined wordmark uses only absolute M, L and Q commands
    /// (verified against the asset), so anything else is deliberately unsupported rather than
    /// half-implemented.
    /// </summary>
    private static GraphicsPath BuildPath(string data)
    {
        var path = new GraphicsPath(FillMode.Winding);
        var tokens = TokenRegex().Matches(data);

        var current = PointF.Empty;
        var start = PointF.Empty;
        var open = false;
        var command = '\0';
        var numbers = new List<float>(4);

        void Flush()
        {
            switch (command)
            {
                case 'M' when numbers.Count >= 2:
                    if (open)
                    {
                        path.CloseFigure();
                    }

                    current = new PointF(numbers[0], numbers[1]);
                    start = current;
                    path.StartFigure();
                    open = true;
                    break;

                case 'L' when numbers.Count >= 2:
                    var lineTo = new PointF(numbers[0], numbers[1]);
                    path.AddLine(current, lineTo);
                    current = lineTo;
                    break;

                case 'Q' when numbers.Count >= 4:
                    // GDI+ has no quadratic primitive; convert to the equivalent cubic.
                    var controlPoint = new PointF(numbers[0], numbers[1]);
                    var end = new PointF(numbers[2], numbers[3]);
                    var c1 = new PointF(
                        current.X + (2f / 3f * (controlPoint.X - current.X)),
                        current.Y + (2f / 3f * (controlPoint.Y - current.Y)));
                    var c2 = new PointF(
                        end.X + (2f / 3f * (controlPoint.X - end.X)),
                        end.Y + (2f / 3f * (controlPoint.Y - end.Y)));
                    path.AddBezier(current, c1, c2, end);
                    current = end;
                    break;

                case 'Z':
                    if (open)
                    {
                        path.CloseFigure();
                        open = false;
                    }

                    current = start;
                    break;
            }

            numbers.Clear();
        }

        foreach (Match token in tokens)
        {
            var text = token.Value;

            if (char.IsLetter(text[0]))
            {
                Flush();
                command = char.ToUpperInvariant(text[0]);

                if (command == 'Z')
                {
                    Flush();
                }

                continue;
            }

            numbers.Add(float.Parse(text, CultureInfo.InvariantCulture));

            // Commands take a fixed number of coordinates and repeat implicitly.
            var arity = command switch { 'M' or 'L' => 2, 'Q' => 4, _ => 0 };
            if (arity > 0 && numbers.Count == arity)
            {
                Flush();

                // An implicit repeat after M continues as L, per the SVG spec.
                if (command == 'M')
                {
                    command = 'L';
                }
            }
        }

        Flush();

        if (open)
        {
            path.CloseFigure();
        }

        return path;
    }

    [GeneratedRegex(@"\sd=""([^""]*)""")]
    private static partial Regex PathDataRegex();

    [GeneratedRegex(@"[A-Za-z]|-?\d*\.?\d+(?:[eE][-+]?\d+)?")]
    private static partial Regex TokenRegex();
}
