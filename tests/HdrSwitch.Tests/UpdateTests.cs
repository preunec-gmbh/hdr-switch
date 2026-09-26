using HdrSwitch.Core.Cli;
using HdrSwitch.Core.Updates;
using Xunit;

namespace HdrSwitch.Tests;

public class UpdateTests
{
    private const string Download = "https://github.com/preunec-gmbh/hdr-switch/releases/download/v1.1.0/";

    private static string ReleaseJson(
        string tag = "v1.1.0",
        bool prerelease = false,
        string exeUrl = Download + "HdrSwitch.exe",
        string? shaUrl = Download + "HdrSwitch.exe.sha256")
    {
        var sha = shaUrl is null
            ? string.Empty
            : $$""", { "name": "HdrSwitch.exe.sha256", "browser_download_url": "{{shaUrl}}" }""";

        return $$"""
            {
              "tag_name": "{{tag}}",
              "html_url": "https://github.com/preunec-gmbh/hdr-switch/releases/tag/{{tag}}",
              "draft": false,
              "prerelease": {{(prerelease ? "true" : "false")}},
              "assets": [
                { "name": "HdrSwitch.exe", "browser_download_url": "{{exeUrl}}" }{{sha}}
              ]
            }
            """;
    }

    [Fact]
    public void ParseRelease_ReadsVersionAndAssets()
    {
        var release = UpdateChecker.ParseRelease(ReleaseJson())!;

        Assert.Equal(new Version(1, 1, 0), release.Version);
        Assert.Equal(Download + "HdrSwitch.exe", release.ExecutableUrl);
        Assert.Equal(Download + "HdrSwitch.exe.sha256", release.ChecksumUrl);
        Assert.EndsWith("/tag/v1.1.0", release.PageUrl);
    }

    [Fact]
    public void ParseRelease_RejectsAReleaseWithoutAChecksum()
    {
        Assert.Null(UpdateChecker.ParseRelease(ReleaseJson(shaUrl: null)));
    }

    [Fact]
    public void ParseRelease_RejectsDownloadsFromAnyOtherHost()
    {
        Assert.Null(UpdateChecker.ParseRelease(ReleaseJson(exeUrl: "https://example.com/HdrSwitch.exe")));
        Assert.Null(UpdateChecker.ParseRelease(
            ReleaseJson(exeUrl: "https://github.com/someone-else/hdr-switch/releases/download/v1.1.0/HdrSwitch.exe")));
    }

    [Fact]
    public void ParseRelease_IgnoresPrereleasesAndBadTags()
    {
        Assert.Null(UpdateChecker.ParseRelease(ReleaseJson(prerelease: true)));
        Assert.Null(UpdateChecker.ParseRelease(ReleaseJson(tag: "nightly")));
    }

    [Theory]
    [InlineData("v1.0.1", 1, 0, 1)]
    [InlineData("1.2.0", 1, 2, 0)]
    [InlineData("v2.0", 2, 0, 0)]
    public void TryParseTag_AcceptsTheTagStyles(string tag, int major, int minor, int build)
    {
        Assert.True(UpdateChecker.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Fact]
    public void IsNewer_ComparesNumerically()
    {
        var release = UpdateChecker.ParseRelease(ReleaseJson(tag: "v1.10.0"))!;

        Assert.True(UpdateChecker.IsNewer(release, new Version(1, 9, 3)));
        Assert.False(UpdateChecker.IsNewer(release, new Version(1, 10, 0)));
        Assert.False(UpdateChecker.IsNewer(release, new Version(2, 0, 0)));
    }

    [Fact]
    public void ParseChecksum_ReadsTheReleaseFormat()
    {
        const string hash = "3f6c0b1f0e7a8f4c9f1d2e3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2b3c";

        // What release.yml writes: "<hash>  HdrSwitch.exe" plus a CRLF.
        Assert.Equal(hash, UpdateInstaller.ParseChecksum($"{hash}  HdrSwitch.exe\r\n", "HdrSwitch.exe"));
        Assert.Equal(hash, UpdateInstaller.ParseChecksum($"{hash.ToUpperInvariant()} *HdrSwitch.exe", "HdrSwitch.exe"));
        Assert.Equal(hash, UpdateInstaller.ParseChecksum(hash, "HdrSwitch.exe"));
    }

    [Fact]
    public void ParseChecksum_RejectsGarbageAndOtherFiles()
    {
        Assert.Null(UpdateInstaller.ParseChecksum("not a hash  HdrSwitch.exe", "HdrSwitch.exe"));
        Assert.Null(UpdateInstaller.ParseChecksum(new string('a', 64) + "  Other.exe", "HdrSwitch.exe"));
        Assert.Null(UpdateInstaller.ParseChecksum("", "HdrSwitch.exe"));
    }

    [Fact]
    public void Swap_PutsTheNewFileInPlaceAndKeepsTheOldOneAside()
    {
        var dir = Directory.CreateTempSubdirectory("hdrswitch-update-").FullName;
        try
        {
            var exe = Path.Combine(dir, "HdrSwitch.exe");
            File.WriteAllText(exe, "old");
            File.WriteAllText(exe + UpdateInstaller.DownloadSuffix, "new");

            UpdateInstaller.Swap(exe, exe + UpdateInstaller.DownloadSuffix);

            Assert.Equal("new", File.ReadAllText(exe));
            Assert.Equal("old", File.ReadAllText(exe + UpdateInstaller.OldSuffix));

            UpdateInstaller.CleanUp(exe);
            Assert.False(File.Exists(exe + UpdateInstaller.OldSuffix));
            Assert.True(File.Exists(exe));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Swap_RestoresTheOriginalWhenTheNewFileIsMissing()
    {
        var dir = Directory.CreateTempSubdirectory("hdrswitch-update-").FullName;
        try
        {
            var exe = Path.Combine(dir, "HdrSwitch.exe");
            File.WriteAllText(exe, "old");

            Assert.ThrowsAny<IOException>(() => UpdateInstaller.Swap(exe, Path.Combine(dir, "missing.download")));

            Assert.Equal("old", File.ReadAllText(exe));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ExtractHighlights_TakesTheBoldTitlesOfTopLevelBullets()
    {
        // The shape release.yml publishes: the version's CHANGELOG section.
        const string body = """
            ### Added

            - **Check for updates, and install in one click.** *Check for updates…* in the tray menu and in
              Settings. When a release is newer, *Update now* downloads it.
            - **Only the shared screen loses HDR.** With more than one HDR display on, the prompt asks.
              - a nested bullet that is not a highlight

            ### Fixed

            - **HDR switched off while the screen picker was still open.** Chromium browsers record a capture.
            - **A fourth item.** Beyond the limit.
            """;

        Assert.Equal(
        [
            "Check for updates, and install in one click",
            "Only the shared screen loses HDR",
            "HDR switched off while the screen picker was still open",
        ], UpdateChecker.ExtractHighlights(body));
    }

    [Fact]
    public void ExtractHighlights_FallsBackToTheFirstSentence()
    {
        Assert.Equal(
            ["Fixed a crash when a display is unplugged"],
            UpdateChecker.ExtractHighlights("- Fixed a crash when a display is unplugged. It no longer takes the tray down."));
    }

    [Fact]
    public void ExtractHighlights_HandlesGitHubsDefaultBody()
    {
        // What the v1.1.0 release carried before release.yml used the CHANGELOG.
        Assert.Empty(UpdateChecker.ExtractHighlights(
            "**Full Changelog**: https://github.com/preunec-gmbh/hdr-switch/compare/v1.0.1...v1.1.0"));
        Assert.Empty(UpdateChecker.ExtractHighlights(null));
    }

    [Fact]
    public void ParseRelease_CarriesTheHighlights()
    {
        var json = ReleaseJson().Replace(
            "\"draft\": false,",
            "\"draft\": false, \"body\": \"### Added\\r\\n\\r\\n- **Tray shows who is sharing.** Details.\",");

        Assert.Equal(["Tray shows who is sharing"], UpdateChecker.ParseRelease(json)!.Highlights);
    }

    [Fact]
    public void AfterUpdateFlag_StartsTheTrayAndCarriesThePid()
    {
        var options = CommandLine.Parse(["--after-update", "4242"]);

        Assert.Equal(CliCommand.Tray, options.Command);
        Assert.Equal(4242, options.AfterUpdatePid);
        Assert.Null(options.Error);

        Assert.NotNull(CommandLine.Parse(["--after-update"]).Error);
        Assert.NotNull(CommandLine.Parse(["--after-update", "abc"]).Error);
    }
}
