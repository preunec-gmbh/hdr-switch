namespace HdrSwitch.Core.Sharing;

/// <summary>Minimal registry surface, abstracted so the reader can be unit tested without HKCU.</summary>
public interface IRegistryProbe
{
    IReadOnlyList<string> GetSubKeyNames(string hkcuPath);

    long? GetQwordValue(string hkcuPath, string valueName);
}

/// <summary>
/// Reads which applications are capturing the screen right now.
///
/// Windows tracks screen capture through the same CapabilityAccessManager consent store it uses
/// for the camera and microphone privacy indicators:
///
///   HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\
///       graphicsCaptureProgrammatic\[NonPackaged\]&lt;app&gt;
///       graphicsCaptureWithoutBorder\[NonPackaged\]&lt;app&gt;
///
/// Each app subkey carries LastUsedTimeStart / LastUsedTimeStop as REG_QWORD FILETIMEs.
/// While a capture is in progress, Stop is zero. That is the whole signal -- no polling of
/// graphics APIs, no injection, no elevation.
/// </summary>
public static class ConsentStoreReader
{
    public const string ConsentStoreRoot =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    public const string ProgrammaticCapability = "graphicsCaptureProgrammatic";
    public const string WithoutBorderCapability = "graphicsCaptureWithoutBorder";

    private const string NonPackagedSubKey = "NonPackaged";
    private const string StartValue = "LastUsedTimeStart";
    private const string StopValue = "LastUsedTimeStop";

    public static IReadOnlyList<(string Name, CaptureCapability Capability)> Capabilities { get; } =
    [
        (ProgrammaticCapability, CaptureCapability.Programmatic),
        (WithoutBorderCapability, CaptureCapability.WithoutBorder),
    ];

    /// <summary>
    /// All apps currently capturing. An app registered under both capabilities appears once,
    /// because the caller cares about "who is capturing", not "how many ways".
    /// </summary>
    public static IReadOnlyList<CaptureSession> GetActiveSessions(
        IRegistryProbe probe,
        Func<string, string?>? productNameLookup = null)
    {
        var byAppKey = new Dictionary<string, CaptureSession>(StringComparer.OrdinalIgnoreCase);

        foreach (var (capabilityName, capability) in Capabilities)
        {
            var capabilityPath = $@"{ConsentStoreRoot}\{capabilityName}";

            // Packaged (Store) apps sit directly under the capability key; Win32 apps sit one
            // level deeper under NonPackaged.
            CollectFrom(probe, capabilityPath, capability, isPackaged: true, productNameLookup, byAppKey);
            CollectFrom(probe, $@"{capabilityPath}\{NonPackagedSubKey}", capability,
                isPackaged: false, productNameLookup, byAppKey);
        }

        return byAppKey.Values.OrderBy(s => s.AppName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectFrom(
        IRegistryProbe probe,
        string path,
        CaptureCapability capability,
        bool isPackaged,
        Func<string, string?>? productNameLookup,
        Dictionary<string, CaptureSession> sink)
    {
        foreach (var subKey in probe.GetSubKeyNames(path))
        {
            // "NonPackaged" is a container, never an app.
            if (subKey.Equals(NonPackagedSubKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keyPath = $@"{path}\{subKey}";
            var start = probe.GetQwordValue(keyPath, StartValue);
            var stop = probe.GetQwordValue(keyPath, StopValue);

            if (!IsActive(start, stop))
            {
                continue;
            }

            var executablePath = isPackaged ? null : DecodeExecutablePath(subKey);
            var appKey = DeriveAppKey(subKey, isPackaged);
            var appName = DeriveAppName(subKey, executablePath, isPackaged, productNameLookup);

            var session = new CaptureSession
            {
                RegistryKey = subKey,
                Capability = capability,
                ExecutablePath = executablePath,
                AppKey = appKey,
                AppName = appName,
                IsPackaged = isPackaged,
                StartedAtUtc = ToDateTime(start),
            };

            // First capability wins; both describe the same capture.
            sink.TryAdd(appKey, session);
        }
    }

    /// <summary>
    /// A capture is in progress when it has started and has no later stop. Windows writes
    /// Stop = 0 while in use; a Stop older than Start means a new capture began before the
    /// previous record was closed out.
    /// </summary>
    public static bool IsActive(long? start, long? stop)
    {
        if (start is null or <= 0)
        {
            return false;
        }

        return stop is null or <= 0 || stop < start;
    }

    /// <summary>
    /// Consent-store subkeys encode the executable path with '#' standing in for the path
    /// separator: "C:#Users#me#AppData#Local#Discord#app-1.0.9254#Discord.exe".
    /// </summary>
    public static string DecodeExecutablePath(string registryKeyName) =>
        registryKeyName.Replace('#', '\\');

    /// <summary>
    /// Rule identity. For Win32 apps this is the executable file name, NOT the full path:
    /// Discord reinstalls into a version-stamped directory on every update, and a path-based
    /// key would quietly forget the user's learned preference each time.
    /// </summary>
    public static string DeriveAppKey(string registryKeyName, bool isPackaged)
    {
        if (isPackaged)
        {
            return registryKeyName.ToLowerInvariant();
        }

        var decoded = DecodeExecutablePath(registryKeyName);
        var fileName = decoded.Contains('\\')
            ? decoded[(decoded.LastIndexOf('\\') + 1)..]
            : decoded;

        return (fileName.Length > 0 ? fileName : decoded).ToLowerInvariant();
    }

    /// <summary>Human-readable name for the suggestion toast.</summary>
    public static string DeriveAppName(
        string registryKeyName,
        string? executablePath,
        bool isPackaged,
        Func<string, string?>? productNameLookup = null)
    {
        if (isPackaged)
        {
            // "Microsoft.SkypeApp_kzf8qxf38zg5c" -> "SkypeApp"
            var family = registryKeyName;
            var underscore = family.LastIndexOf('_');
            if (underscore > 0)
            {
                family = family[..underscore];
            }

            var lastDot = family.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < family.Length - 1)
            {
                family = family[(lastDot + 1)..];
            }

            return family.Length > 0 ? family : registryKeyName;
        }

        if (executablePath is { Length: > 0 } && productNameLookup is not null)
        {
            var product = productNameLookup(executablePath);
            if (!string.IsNullOrWhiteSpace(product))
            {
                return product.Trim();
            }
        }

        var fileName = executablePath is { Length: > 0 }
            ? Path.GetFileNameWithoutExtension(executablePath)
            : registryKeyName;

        return string.IsNullOrWhiteSpace(fileName) ? registryKeyName : fileName;
    }

    private static DateTime? ToDateTime(long? fileTime)
    {
        if (fileTime is null or <= 0)
        {
            return null;
        }

        try
        {
            return DateTime.FromFileTimeUtc(fileTime.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A corrupt or out-of-range stamp should not take the watcher down.
            return null;
        }
    }
}
