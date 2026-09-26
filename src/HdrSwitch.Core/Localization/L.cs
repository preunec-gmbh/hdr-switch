using System.Globalization;
using System.Runtime.InteropServices;

namespace HdrSwitch.Core.Localization;

/// <summary>The interface language. <see cref="Automatic"/> follows the Windows display language.</summary>
public enum UiLanguage
{
    Automatic = 0,
    English = 1,
    German = 2,
    Turkish = 3,
}

/// <summary>
/// Interface text in English, German and Turkish.
///
/// The English text is the key: <c>L.T("Keep HDR")</c> reads as what it shows, and a string with
/// no translation falls back to English instead of showing a key. Formatted text goes through
/// <see cref="F"/> with numbered placeholders, because word order differs between the languages
/// ("{0} is sharing your screen" / "{0} ekranınızı paylaşıyor").
///
/// Not resx satellite assemblies: the app runs with InvariantGlobalization, under which
/// culture-based resource lookup does not work, and a single-file build is simpler with the
/// tables compiled in. LocalizationTests checks every L.T / L.F literal in the source has both
/// translations with the same placeholders.
///
/// The command line never calls <see cref="Use"/>, so its output stays English for scripts.
/// </summary>
public static class L
{
    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();

    private const int LangGerman = 0x07;
    private const int LangTurkish = 0x1F;

    /// <summary>The language in effect. Never <see cref="UiLanguage.Automatic"/>.</summary>
    public static UiLanguage Current { get; private set; } = UiLanguage.English;

    public static void Use(UiLanguage requested) =>
        Current = requested == UiLanguage.Automatic ? Detect() : requested;

    /// <summary>The Windows display language, when it is one HDR Switch speaks.</summary>
    public static UiLanguage Detect()
    {
        try
        {
            // The low 10 bits of a LANGID are the primary language.
            return (GetUserDefaultUILanguage() & 0x3FF) switch
            {
                LangGerman => UiLanguage.German,
                LangTurkish => UiLanguage.Turkish,
                _ => UiLanguage.English,
            };
        }
        catch (EntryPointNotFoundException)
        {
            return UiLanguage.English;
        }
    }

    /// <summary>Translate a fixed string.</summary>
    public static string T(string english) => Lookup(english, Current);

    /// <summary>Translate a composite format string, then fill it in.</summary>
    public static string F(string englishFormat, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Lookup(englishFormat, Current), args);

    /// <summary>The table for a language; null for English. Exposed for LocalizationTests.</summary>
    public static IReadOnlyDictionary<string, string>? TableFor(UiLanguage language) => language switch
    {
        UiLanguage.German => Translations.German,
        UiLanguage.Turkish => Translations.Turkish,
        _ => null,
    };

    /// <summary>What the language picker shows: each language in its own name.</summary>
    public static string NativeName(UiLanguage language) => language switch
    {
        UiLanguage.English => "English",
        UiLanguage.German => "Deutsch",
        UiLanguage.Turkish => "Türkçe",
        _ => L.T("Automatic (Windows language)"),
    };

    private static string Lookup(string english, UiLanguage language) =>
        TableFor(language) is { } table && table.TryGetValue(english, out var translated) ? translated : english;
}
