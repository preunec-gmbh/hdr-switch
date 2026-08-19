using System.Drawing.Text;
using Microsoft.Win32;

namespace HdrSwitch.Ui;

/// <summary>
/// The preunec design tokens, vendored from
/// <c>design-system-kit/package/tokens/preunec.css</c> (see <c>Brand/VENDORED.md</c>).
///
/// The kit ships CSS custom properties, which a WinForms app cannot consume, so the semantic
/// roles are mirrored here by hand. They are mirrored as *roles*, not as raw colours: code asks
/// for <see cref="TextPrimary"/> or <see cref="AccentInteractive"/> and gets the right value for
/// whichever polarity is active, exactly as the kit intends ("do not assume a surface's
/// polarity"). Re-sync by copying the values again, never by inventing new ones.
/// </summary>
internal static class Brand
{
    // ---------------------------------------------------------------- palette (brand/colour.md)

    /// <summary>Deep Navy — structure, seriousness, the brand itself.</summary>
    internal static readonly Color Navy = FromHex("#0D112F");

    /// <summary>Deep Blue — navigation, primary action, trust.</summary>
    internal static readonly Color DeepBlue = FromHex("#233083");

    /// <summary>Vivid Cyan — technology, software, interactivity. Fill and focus only, never text.</summary>
    internal static readonly Color Cyan = FromHex("#0693E3");

    /// <summary>Bright Teal — success, action. Fill only.</summary>
    internal static readonly Color Teal = FromHex("#00D084");

    /// <summary>Light Grey — neutral ground.</summary>
    internal static readonly Color LightGrey = FromHex("#F5F6FA");

    // ---------------------------------------------------------------- polarity

    private const string PersonalizeKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Follows the Windows app theme. Both polarities are first-class in the kit.</summary>
    internal static bool IsDark
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    private static Color Pick(string light, string dark) => FromHex(IsDark ? dark : light);

    // ---------------------------------------------------------------- surfaces

    internal static Color SurfaceBase => Pick("#FFFFFF", "#0D112F");

    internal static Color SurfaceRaised => Pick("#F5F6FA", "#161D4A");

    internal static Color SurfaceOverlay => Pick("#FFFFFF", "#1A2150");

    internal static Color SurfaceSunken => Pick("#ECEEF7", "#080B22");

    // ---------------------------------------------------------------- borders

    internal static Color BorderSubtle => Pick("#E5E7F1", "#262E63");

    internal static Color BorderStrong => Pick("#7B85AF", "#616AA2");

    // ---------------------------------------------------------------- text

    internal static Color TextPrimary => Pick("#0D112F", "#F5F6FA");

    internal static Color TextSecondary => Pick("#4B5180", "#A9B0D6");

    internal static Color TextMuted => Pick("#6E76A8", "#6E76A8");

    internal static Color TextDisabled => Pick("#9CA3C4", "#4B5180");

    internal static Color TextInverse => Pick("#FFFFFF", "#0D112F");

    // ---------------------------------------------------------------- accent

    /// <summary>Primary action. Deep Blue on light, Vivid Cyan on dark.</summary>
    internal static Color AccentInteractive => Pick("#233083", "#0693E3");

    internal static Color AccentInteractiveFg => Pick("#FFFFFF", "#0D112F");

    internal static Color AccentInteractiveHover => Pick("#0D112F", "#3AA9E8");

    /// <summary>The darkened cyan that is legal for text. Never use raw cyan for a label.</summary>
    internal static Color AccentInk => Pick("#0B6FA8", "#0693E3");

    internal static Color FocusRing => FromHex("#0693E3");

    // ---------------------------------------------------------------- state

    internal static Color StateSuccess => Pick("#00734A", "#00D084");

    internal static Color StateSuccessFill => FromHex("#00D084");

    internal static Color StateWarning => Pick("#8A6200", "#FCB900");

    internal static Color StateWarningFill => FromHex("#FCB900");

    internal static Color StateDanger => Pick("#C5262B", "#F4676B");

    internal static Color StateDangerFill => FromHex("#C5262B");

    internal static Color StateNeutral => Pick("#4B5180", "#A9B0D6");

    // ---------------------------------------------------------------- gradient

    /// <summary>
    /// The sanctioned <c>brand</c> gradient: navy → blue → cyan at 135°. Backgrounds only —
    /// never behind text that must stay readable across the whole sweep.
    /// </summary>
    internal static Color[] BrandGradient => [Navy, DeepBlue, Cyan];

    /// <summary>The sanctioned <c>brand-soft</c> gradient: blue → cyan → teal.</summary>
    internal static Color[] BrandSoftGradient => [DeepBlue, Cyan, Teal];

    /// <summary>
    /// Gradient for an accent stripe, chosen by polarity. On a navy surface the brand gradient's
    /// navy end is invisible and the stripe looks half-painted, so dark surfaces get brand-soft,
    /// which stays legible against navy. Both are sanctioned gradients.
    /// </summary>
    internal static Color[] AccentStripeGradient => IsDark ? BrandSoftGradient : BrandGradient;

    // ---------------------------------------------------------------- typography

    /// <summary>
    /// Body face. The kit specifies Inter, which is a web font and is not installed on a stock
    /// Windows machine, so the stack degrades through Segoe UI rather than silently landing on
    /// whatever GDI+ picks when a family is missing.
    /// </summary>
    private static readonly string[] BodyStack =
        ["Inter", "Segoe UI Variable Text", "Segoe UI", "Tahoma"];

    /// <summary>Display face for headings. Plus Jakarta Sans per the kit, then the body stack.</summary>
    private static readonly string[] DisplayStack =
        ["Plus Jakarta Sans", "Inter", "Segoe UI Variable Display", "Segoe UI", "Tahoma"];

    private static readonly string[] MonoStack =
        ["Cascadia Mono", "Consolas", "Courier New"];

    private static readonly Lazy<HashSet<string>> Installed = new(() =>
    {
        try
        {
            using var collection = new InstalledFontCollection();
            return collection.Families
                .Select(f => f.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    });

    private static string Resolve(string[] stack) =>
        stack.FirstOrDefault(Installed.Value.Contains) ?? stack[^1];

    internal static Font Body(float size, FontStyle style = FontStyle.Regular) =>
        new(Resolve(BodyStack), size, style, GraphicsUnit.Point);

    internal static Font Display(float size, FontStyle style = FontStyle.Bold) =>
        new(Resolve(DisplayStack), size, style, GraphicsUnit.Point);

    internal static Font Mono(float size, FontStyle style = FontStyle.Regular) =>
        new(Resolve(MonoStack), size, style, GraphicsUnit.Point);

    /// <summary>Reported by `selftest` so a missing brand face is visible rather than guessed at.</summary>
    internal static string FontReport =>
        $"body={Resolve(BodyStack)}, display={Resolve(DisplayStack)}, mono={Resolve(MonoStack)}";

    private static Color FromHex(string hex)
    {
        var value = hex.TrimStart('#');
        return Color.FromArgb(
            255,
            Convert.ToInt32(value.Substring(0, 2), 16),
            Convert.ToInt32(value.Substring(2, 2), 16),
            Convert.ToInt32(value.Substring(4, 2), 16));
    }
}
