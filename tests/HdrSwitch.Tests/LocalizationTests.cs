using System.Text.RegularExpressions;
using HdrSwitch.Core.Localization;
using Xunit;

namespace HdrSwitch.Tests;

/// <summary>
/// Reads the source tree rather than a list kept by hand: every L.T / L.F literal in src/ must
/// have a German and a Turkish translation with the same placeholders, and every translation must
/// still be used. A new string without translations fails here, not in front of a user.
/// </summary>
[Collection(nameof(LanguageSwitchingCollection))]
public partial class LocalizationTests
{
    private static readonly UiLanguage[] Translated = [UiLanguage.German, UiLanguage.Turkish];

    [GeneratedRegex(@"\bL\.[TF]\(\s*""((?:[^""\\]|\\.)*)""")]
    private static partial Regex Call();

    [GeneratedRegex(@"\bL\.[TF]\(")]
    private static partial Regex AnyCall();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HdrSwitch.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src");
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(SourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.EndsWith("Translations.cs", StringComparison.Ordinal));

    /// <summary>The keys as the runtime sees them: C# escapes resolved.</summary>
    private static HashSet<string> SourceKeys() =>
        SourceFiles()
            .SelectMany(f => Call().Matches(File.ReadAllText(f)).Select(m => Regex.Unescape(m.Groups[1].Value)))
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void TheScannerFindsTheStrings()
    {
        // Guards the guard: if the regex broke, every other test here would pass vacuously.
        var keys = SourceKeys();
        Assert.True(keys.Count > 100, $"only {keys.Count} strings found");
        Assert.Contains("Keep HDR", keys);
        Assert.Contains("{0} is sharing your screen", keys);
    }

    [Fact]
    public void EveryCallPassesASingleLiteral()
    {
        // L.T(variable) or L.T("a" + "b") cannot be checked for a translation.
        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            Assert.True(
                AnyCall().Matches(text).Count == Call().Matches(text).Count,
                $"{Path.GetFileName(file)} has an L.T / L.F call whose argument is not a single string literal");
        }
    }

    [Fact]
    public void EveryStringIsTranslated()
    {
        var keys = SourceKeys();

        foreach (var language in Translated)
        {
            var table = L.TableFor(language)!;
            var missing = keys.Where(k => !table.ContainsKey(k)).ToList();
            Assert.True(missing.Count == 0, $"{language} is missing:\n" + string.Join("\n", missing));
        }
    }

    [Fact]
    public void NoTranslationIsOrphaned()
    {
        var keys = SourceKeys();

        foreach (var language in Translated)
        {
            var unused = L.TableFor(language)!.Keys.Where(k => !keys.Contains(k)).ToList();
            Assert.True(unused.Count == 0, $"{language} translates strings the source no longer uses:\n" + string.Join("\n", unused));
        }
    }

    [Fact]
    public void PlaceholdersMatch()
    {
        foreach (var language in Translated)
        {
            foreach (var (english, translated) in L.TableFor(language)!)
            {
                var expected = Placeholder().Matches(english).Select(m => m.Value).Order().ToList();
                var actual = Placeholder().Matches(translated).Select(m => m.Value).Order().ToList();
                Assert.True(expected.SequenceEqual(actual), $"{language}: placeholders differ for \"{english}\"");
            }
        }
    }

    [Fact]
    public void FileDialogFiltersKeepTheirShape()
    {
        const string filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*";

        foreach (var language in Translated)
        {
            var parts = L.TableFor(language)![filter].Split('|');
            Assert.Equal(4, parts.Length);
            Assert.Equal("*.exe", parts[1]);
            Assert.Equal("*.*", parts[3]);
        }
    }

    [Fact]
    public void UnknownTextFallsBackToEnglish()
    {
        try
        {
            L.Use(UiLanguage.Turkish);
            Assert.Equal("HDR'yi koru", L.T("Keep HDR"));
            Assert.Equal("Discord ekranınızı paylaşıyor", L.F("{0} is sharing your screen", "Discord"));
            Assert.Equal("not a known string", L.T("not a known string"));
        }
        finally
        {
            L.Use(UiLanguage.English);
        }

        Assert.Equal("Keep HDR", L.T("Keep HDR"));
    }
}

/// <summary>
/// L.Current is process-wide. Tests that switch it must not overlap the tests that assert
/// English messages, so this collection runs on its own.
/// </summary>
[CollectionDefinition(nameof(LanguageSwitchingCollection), DisableParallelization = true)]
public sealed class LanguageSwitchingCollection;
