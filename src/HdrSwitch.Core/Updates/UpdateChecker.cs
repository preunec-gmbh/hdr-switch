using System.Net.Http.Headers;
using System.Text.Json;

namespace HdrSwitch.Core.Updates;

/// <summary>A published release that can be installed.</summary>
public sealed record ReleaseInfo
{
    public required Version Version { get; init; }

    public required string Tag { get; init; }

    /// <summary>The release page, for "what's new".</summary>
    public required string PageUrl { get; init; }

    public required string ExecutableUrl { get; init; }

    /// <summary>The published "&lt;sha256&gt;  HdrSwitch.exe" file for the same release.</summary>
    public required string ChecksumUrl { get; init; }
}

/// <summary>
/// Asks GitHub for the latest release of HDR Switch.
///
/// One unauthenticated GET to the public releases API; nothing is sent beyond what any HTTP
/// request carries (no identifiers, no telemetry). Drafts and pre-releases are never offered,
/// because /releases/latest excludes them.
/// </summary>
public static class UpdateChecker
{
    public const string Repository = "preunec-gmbh/hdr-switch";
    public const string LatestReleaseApi = $"https://api.github.com/repos/{Repository}/releases/latest";
    public const string ReleasesPage = $"https://github.com/{Repository}/releases/latest";

    public const string ExecutableAsset = "HdrSwitch.exe";
    public const string ChecksumAsset = "HdrSwitch.exe.sha256";

    /// <summary>
    /// Downloads are only accepted from this repository's own release storage, so a tampered
    /// API response cannot point the updater at an arbitrary host.
    /// </summary>
    public const string AllowedDownloadPrefix = $"https://github.com/{Repository}/releases/download/";

    /// <summary>
    /// The running version, as the three-part number releases are tagged with. Read from the
    /// executable, not this library: only HdrSwitch.csproj carries &lt;Version&gt;, so Core itself
    /// is always 1.0.0 and would offer every user the release they already have.
    /// </summary>
    public static Version CurrentVersion
    {
        get
        {
            var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(UpdateChecker).Assembly;
            var version = assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
        }
    }

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // GitHub rejects API requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HdrSwitch", CurrentVersion.ToString()));
        return client;
    }

    /// <summary>The latest release, or null when it lacks the assets an update needs.</summary>
    public static async Task<ReleaseInfo?> GetLatestAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseRelease(json);
    }

    /// <summary>Parses a GitHub release object. Pure; unit tested.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        if (tag is null || !TryParseTag(tag, out var version))
        {
            return null;
        }

        string? exeUrl = null;
        string? shaUrl = null;

        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;

                if (url is null || !url.StartsWith(AllowedDownloadPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(name, ExecutableAsset, StringComparison.OrdinalIgnoreCase))
                {
                    exeUrl = url;
                }
                else if (string.Equals(name, ChecksumAsset, StringComparison.OrdinalIgnoreCase))
                {
                    shaUrl = url;
                }
            }
        }

        // Without a checksum there is nothing to verify the download against, so it is not an
        // installable release -- the user can still get it from the release page.
        if (exeUrl is null || shaUrl is null)
        {
            return null;
        }

        var page = root.TryGetProperty("html_url", out var html) ? html.GetString() : null;

        return new ReleaseInfo
        {
            Version = version,
            Tag = tag,
            PageUrl = page ?? ReleasesPage,
            ExecutableUrl = exeUrl,
            ChecksumUrl = shaUrl,
        };
    }

    /// <summary>"v1.2.3" or "1.2.3" to a three-part version.</summary>
    public static bool TryParseTag(string tag, out Version version)
    {
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        if (Version.TryParse(text, out var parsed) && parsed.Major >= 0)
        {
            version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));
            return true;
        }

        version = new Version(0, 0, 0);
        return false;
    }

    public static bool IsNewer(ReleaseInfo release, Version current) => release.Version > current;
}
