using System.Diagnostics;
using System.Runtime.InteropServices;
using HdrSwitch.Core.Interop;

namespace HdrSwitch.Core.Hdr;

public interface IHdrController
{
    /// <summary>Which CCD generation is in use. Resolved on first enumeration.</summary>
    HdrApiPath ApiPath { get; }

    IReadOnlyList<DisplayTarget> GetDisplays();

    /// <summary>Re-read one display's current state.</summary>
    DisplayTarget? Refresh(DisplayTarget target);

    HdrSetResult SetHdr(DisplayTarget target, bool enable);
}

/// <summary>
/// Reads and writes per-display HDR state through the Windows CCD API.
///
/// Two API generations exist and both are live in the wild:
///   * Modern (Windows 11 24H2+): GET_ADVANCED_COLOR_INFO_2 / SET_HDR_STATE. Distinguishes
///     true HDR from wide-colour gamut.
///   * Legacy (Windows 10 1803+): GET_ADVANCED_COLOR_INFO / SET_ADVANCED_COLOR_STATE. Conflates
///     HDR and WCG under one "advanced colour" flag.
///
/// The modern path is probed once and cached. Setting HDRSWITCH_FORCE_LEGACY=1 pins the legacy
/// path, so the fallback can actually be exercised on a modern machine instead of being assumed
/// to work -- a fallback nobody has watched fail is not a fallback.
/// </summary>
public sealed class HdrController : IHdrController
{
    public const string ForceLegacyEnvVar = "HDRSWITCH_FORCE_LEGACY";

    private const int MaxQueryRetries = 5;

    /// <summary>
    /// Changing HDR renegotiates the display link; the panel blanks and the new state is not
    /// readable immediately. Measured at roughly 1-3 seconds on a DisplayPort monitor, so the
    /// state is polled rather than read once.
    /// </summary>
    private const int SettleBudgetMs = 4000;

    /// <summary>Shorter probe used when a setter reported an error but may still have applied.</summary>
    private const int ErrorProbeMs = 800;

    private const int SettlePollMs = 75;

    private HdrApiPath _apiPath = HdrApiPath.Unknown;
    private readonly bool _forceLegacy;

    /// <summary>
    /// Set when the modern setter proves ineffective on this system. Kept separate from
    /// <see cref="_apiPath"/> on purpose: the modern *reader* is strictly better (it separates
    /// HDR from wide-colour gamut), so a write-side fallback must not degrade reads.
    /// </summary>
    private bool _preferLegacyWrites;

    public HdrController(bool? forceLegacy = null)
    {
        _forceLegacy = forceLegacy ?? IsTruthy(Environment.GetEnvironmentVariable(ForceLegacyEnvVar));
        if (_forceLegacy)
        {
            _apiPath = HdrApiPath.Legacy;
        }
    }

    private static bool IsTruthy(string? value) =>
        value is not null &&
        (value.Equals("1", StringComparison.Ordinal) ||
         value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
         value.Equals("yes", StringComparison.OrdinalIgnoreCase));

    public HdrApiPath ApiPath => _apiPath;

    /// <summary>True when the legacy path was pinned by environment rather than by probing.</summary>
    public bool LegacyForced => _forceLegacy;

    public IReadOnlyList<DisplayTarget> GetDisplays()
    {
        if (!TryQueryPaths(out var paths))
        {
            return Array.Empty<DisplayTarget>();
        }

        var results = new List<DisplayTarget>(paths.Length);
        var seen = new HashSet<(long, uint)>();

        foreach (var path in paths)
        {
            var adapterId = path.TargetInfo.AdapterId;
            var targetId = path.TargetInfo.Id;

            // Clone/duplicate topologies can list the same physical target more than once.
            if (!seen.Add((adapterId.ToInt64(), targetId)))
            {
                continue;
            }

            var target = BuildTarget(adapterId, targetId, results.Count);
            if (target is not null)
            {
                results.Add(target);
            }
        }

        return results;
    }

    public DisplayTarget? Refresh(DisplayTarget target)
    {
        var adapterId = Luid.FromInt64(target.AdapterId);
        var rebuilt = BuildTarget(adapterId, target.TargetId, target.Index);

        // A LUID can go stale across a driver restart; fall back to matching by stable identity.
        if (rebuilt is null || rebuilt.StableId != target.StableId)
        {
            return GetDisplays().FirstOrDefault(d => d.StableId == target.StableId) ?? rebuilt;
        }

        return rebuilt;
    }

    public HdrSetResult SetHdr(DisplayTarget target, bool enable)
    {
        if (target.Capability == HdrCapability.Unsupported)
        {
            return new HdrSetResult
            {
                Target = target,
                Requested = enable,
                Actual = target.HdrEnabled,
                Win32Error = 0,
                Message = $"{target.Label} does not support HDR.",
            };
        }

        var adapterId = Luid.FromInt64(target.AdapterId);
        var stopwatch = Stopwatch.StartNew();
        var error = ApplyHdrState(adapterId, target.TargetId, enable, out var settled);
        stopwatch.Stop();

        // The observed state is the truth, not the return code. On this machine SET_HDR_STATE
        // returns ERROR_ACCESS_DENIED when enabling HDR and applies the change anyway; treating
        // the return code as authoritative would report a failure that did not happen.
        var after = Refresh(target);
        var actual = after?.HdrEnabled ?? target.HdrEnabled;
        var success = actual == enable;

        string? message = null;
        if (!success)
        {
            message = error != 0
                ? DescribeError(error, target)
                : $"Windows accepted the change but {target.Label} stayed " +
                  $"{(actual ? "on" : "off")}. The display or link may not allow it right now.";
        }

        return new HdrSetResult
        {
            Target = after ?? target,
            Requested = enable,
            Actual = actual,
            Win32Error = error,
            SettleMilliseconds = (int)stopwatch.ElapsedMilliseconds,
            Settled = settled,
            Message = message,
        };
    }

    private static string DescribeError(int error, DisplayTarget target) => error switch
    {
        DisplayConfigNative.ERROR_INVALID_PARAMETER =>
            $"Windows rejected the HDR request for {target.Label} (invalid parameter). " +
            "This usually means the display no longer matches the cached configuration -- rescan and retry.",
        DisplayConfigNative.ERROR_NOT_SUPPORTED =>
            $"{target.Label} reports HDR support but the driver refused the request.",
        DisplayConfigNative.ERROR_ACCESS_DENIED =>
            $"Access denied changing HDR on {target.Label}.",
        DisplayConfigNative.ERROR_GEN_FAILURE =>
            $"The display driver failed the HDR request for {target.Label}.",
        _ => $"Changing HDR on {target.Label} failed with Win32 error {error}.",
    };

    private int ApplyHdrState(Luid adapterId, uint targetId, bool enable, out bool settled)
    {
        EnsureApiPathResolved(adapterId, targetId);

        var modernError = 0;

        if (!_preferLegacyWrites && _apiPath == HdrApiPath.Modern)
        {
            var packet = new DisplayConfigSetHdrState
            {
                Header = MakeHeader(DisplayConfigDeviceInfoType.SetHdrState,
                    Marshal.SizeOf<DisplayConfigSetHdrState>(), adapterId, targetId),
                Value = enable ? 1u : 0u,
            };
            modernError = DisplayConfigNative.DisplayConfigSetDeviceInfo(ref packet);

            // Give a clean call the full settle budget; give a failed one only a short probe,
            // because it may have applied anyway and we do not want to stall before falling back.
            var budget = modernError == DisplayConfigNative.ERROR_SUCCESS ? SettleBudgetMs : ErrorProbeMs;
            if (WaitForState(adapterId, targetId, enable, budget))
            {
                settled = true;
                return modernError;
            }
        }

        // Setting the same value twice is idempotent, so retrying through the legacy setter is
        // safe even if the modern one is still settling.
        var legacy = new DisplayConfigSetAdvancedColorState
        {
            Header = MakeHeader(DisplayConfigDeviceInfoType.SetAdvancedColorState,
                Marshal.SizeOf<DisplayConfigSetAdvancedColorState>(), adapterId, targetId),
            Value = enable ? 1u : 0u,
        };
        var legacyError = DisplayConfigNative.DisplayConfigSetDeviceInfo(ref legacy);

        settled = WaitForState(adapterId, targetId, enable, SettleBudgetMs);

        if (settled && modernError != DisplayConfigNative.ERROR_SUCCESS && !_preferLegacyWrites
            && _apiPath == HdrApiPath.Modern)
        {
            // The modern setter did not do the job but the legacy one did. Keep reading through
            // the modern API and write through the legacy one from now on.
            _preferLegacyWrites = true;
        }

        return legacyError != DisplayConfigNative.ERROR_SUCCESS ? legacyError : modernError;
    }

    /// <summary>Polls the display until it reports the requested state, or the budget runs out.</summary>
    private bool WaitForState(Luid adapterId, uint targetId, bool desired, int budgetMs)
    {
        var deadline = Environment.TickCount64 + budgetMs;

        while (true)
        {
            if (ReadHdrEnabled(adapterId, targetId) == desired)
            {
                return true;
            }

            if (Environment.TickCount64 >= deadline)
            {
                return false;
            }

            Thread.Sleep(SettlePollMs);
        }
    }

    /// <summary>Lightweight single-target read used by the settle loop.</summary>
    private bool? ReadHdrEnabled(Luid adapterId, uint targetId)
    {
        if (_apiPath == HdrApiPath.Modern)
        {
            var info = new DisplayConfigGetAdvancedColorInfo2
            {
                Header = MakeHeader(DisplayConfigDeviceInfoType.GetAdvancedColorInfo2,
                    Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo2>(), adapterId, targetId),
            };
            return DisplayConfigNative.DisplayConfigGetDeviceInfo(ref info) == DisplayConfigNative.ERROR_SUCCESS
                ? info.HighDynamicRangeUserEnabled
                : null;
        }

        var legacy = new DisplayConfigGetAdvancedColorInfo
        {
            Header = MakeHeader(DisplayConfigDeviceInfoType.GetAdvancedColorInfo,
                Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(), adapterId, targetId),
        };
        return DisplayConfigNative.DisplayConfigGetDeviceInfo(ref legacy) == DisplayConfigNative.ERROR_SUCCESS
            ? legacy.AdvancedColorEnabled
            : null;
    }

    private DisplayTarget? BuildTarget(Luid adapterId, uint targetId, int index)
    {
        var (friendlyName, devicePath) = QueryTargetName(adapterId, targetId);

        EnsureApiPathResolved(adapterId, targetId);

        HdrCapability capability;
        bool enabled;
        bool? wideColor = null;
        uint bits;
        uint encoding;
        uint rawFlags;

        if (_apiPath == HdrApiPath.Modern)
        {
            var info = new DisplayConfigGetAdvancedColorInfo2
            {
                Header = MakeHeader(DisplayConfigDeviceInfoType.GetAdvancedColorInfo2,
                    Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo2>(), adapterId, targetId),
            };
            if (DisplayConfigNative.DisplayConfigGetDeviceInfo(ref info) != DisplayConfigNative.ERROR_SUCCESS)
            {
                return null;
            }

            capability =
                !info.HighDynamicRangeSupported ? HdrCapability.Unsupported
                : info.AdvancedColorLimitedByPolicy ? HdrCapability.BlockedByPolicy
                : HdrCapability.Supported;
            enabled = info.HighDynamicRangeUserEnabled;
            wideColor = info.WideColorUserEnabled;
            bits = info.BitsPerColorChannel;
            encoding = info.ColorEncoding;
            rawFlags = info.Value;
        }
        else
        {
            var info = new DisplayConfigGetAdvancedColorInfo
            {
                Header = MakeHeader(DisplayConfigDeviceInfoType.GetAdvancedColorInfo,
                    Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(), adapterId, targetId),
            };
            if (DisplayConfigNative.DisplayConfigGetDeviceInfo(ref info) != DisplayConfigNative.ERROR_SUCCESS)
            {
                return null;
            }

            capability =
                !info.AdvancedColorSupported ? HdrCapability.Unsupported
                : info.AdvancedColorForceDisabled ? HdrCapability.BlockedByPolicy
                : HdrCapability.Supported;
            // The legacy flag cannot separate HDR from wide-colour gamut; this is the known
            // cost of the fallback path and is reported as-is.
            enabled = info.AdvancedColorEnabled;
            bits = info.BitsPerColorChannel;
            encoding = info.ColorEncoding;
            rawFlags = info.Value;
        }

        return new DisplayTarget
        {
            Index = index,
            FriendlyName = friendlyName,
            DevicePath = devicePath,
            AdapterId = adapterId.ToInt64(),
            TargetId = targetId,
            Capability = capability,
            HdrEnabled = enabled,
            WideColorEnabled = wideColor,
            BitsPerColorChannel = bits,
            ColorEncoding = DescribeEncoding(encoding),
            SdrWhiteLevelNits = enabled ? QuerySdrWhiteLevel(adapterId, targetId) : null,
            RawFlags = rawFlags,
        };
    }

    private void EnsureApiPathResolved(Luid adapterId, uint targetId)
    {
        if (_apiPath != HdrApiPath.Unknown)
        {
            return;
        }

        var probe = new DisplayConfigGetAdvancedColorInfo2
        {
            Header = MakeHeader(DisplayConfigDeviceInfoType.GetAdvancedColorInfo2,
                Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo2>(), adapterId, targetId),
        };
        var error = DisplayConfigNative.DisplayConfigGetDeviceInfo(ref probe);
        _apiPath = error == DisplayConfigNative.ERROR_SUCCESS ? HdrApiPath.Modern : HdrApiPath.Legacy;
    }

    private static (string FriendlyName, string DevicePath) QueryTargetName(Luid adapterId, uint targetId)
    {
        var packet = new DisplayConfigTargetDeviceName
        {
            Header = MakeHeader(DisplayConfigDeviceInfoType.GetTargetName,
                Marshal.SizeOf<DisplayConfigTargetDeviceName>(), adapterId, targetId),
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty,
        };

        if (DisplayConfigNative.DisplayConfigGetDeviceInfo(ref packet) != DisplayConfigNative.ERROR_SUCCESS)
        {
            return (string.Empty, string.Empty);
        }

        return (packet.MonitorFriendlyDeviceName?.Trim() ?? string.Empty,
                packet.MonitorDevicePath?.Trim() ?? string.Empty);
    }

    private static double? QuerySdrWhiteLevel(Luid adapterId, uint targetId)
    {
        var packet = new DisplayConfigSdrWhiteLevel
        {
            Header = MakeHeader(DisplayConfigDeviceInfoType.GetSdrWhiteLevel,
                Marshal.SizeOf<DisplayConfigSdrWhiteLevel>(), adapterId, targetId),
        };

        return DisplayConfigNative.DisplayConfigGetDeviceInfo(ref packet) == DisplayConfigNative.ERROR_SUCCESS
            ? packet.Nits
            : null;
    }

    private static DisplayConfigDeviceInfoHeader MakeHeader(
        DisplayConfigDeviceInfoType type, int size, Luid adapterId, uint targetId) => new()
        {
            Type = type,
            Size = (uint)size,
            AdapterId = adapterId,
            Id = targetId,
        };

    /// <summary>DISPLAYCONFIG_COLOR_ENCODING (wingdi.h).</summary>
    private static string DescribeEncoding(uint encoding) => encoding switch
    {
        0 => "RGB",
        1 => "YCbCr444",
        2 => "YCbCr422",
        3 => "YCbCr420",
        4 => "Intensity",
        _ => $"encoding {encoding}",
    };

    private static bool TryQueryPaths(out DisplayConfigPathInfo[] paths)
    {
        // The display set can change between sizing and querying, which surfaces as
        // ERROR_INSUFFICIENT_BUFFER. Retry rather than reporting a spurious failure.
        for (var attempt = 0; attempt < MaxQueryRetries; attempt++)
        {
            var status = DisplayConfigNative.GetDisplayConfigBufferSizes(
                DisplayConfigNative.QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount);
            if (status != DisplayConfigNative.ERROR_SUCCESS)
            {
                paths = Array.Empty<DisplayConfigPathInfo>();
                return false;
            }

            var pathArray = new DisplayConfigPathInfo[pathCount];
            var modeArray = new DisplayConfigModeInfo[modeCount];

            status = DisplayConfigNative.QueryDisplayConfig(
                DisplayConfigNative.QDC_ONLY_ACTIVE_PATHS,
                ref pathCount, pathArray, ref modeCount, modeArray, IntPtr.Zero);

            if (status == DisplayConfigNative.ERROR_SUCCESS)
            {
                // QueryDisplayConfig writes back the number of elements it actually filled.
                paths = pathCount < pathArray.Length ? pathArray[..(int)pathCount] : pathArray;
                return true;
            }

            if (status != DisplayConfigNative.ERROR_INSUFFICIENT_BUFFER)
            {
                paths = Array.Empty<DisplayConfigPathInfo>();
                return false;
            }
        }

        paths = Array.Empty<DisplayConfigPathInfo>();
        return false;
    }

    /// <summary>
    /// Marshalled sizes of every interop struct against the sizes the SDK header implies.
    /// A silent layout mistake here would produce plausible-looking garbage rather than an
    /// error, so `HdrSwitch.exe selftest` prints this table.
    /// </summary>
    public static IReadOnlyList<(string Struct, int Actual, int Expected)> LayoutCheck() =>
    [
        ("DISPLAYCONFIG_DEVICE_INFO_HEADER", Marshal.SizeOf<DisplayConfigDeviceInfoHeader>(), 20),
        ("DISPLAYCONFIG_PATH_INFO", Marshal.SizeOf<DisplayConfigPathInfo>(), 72),
        ("DISPLAYCONFIG_MODE_INFO", Marshal.SizeOf<DisplayConfigModeInfo>(), 64),
        ("DISPLAYCONFIG_TARGET_DEVICE_NAME", Marshal.SizeOf<DisplayConfigTargetDeviceName>(), 420),
        ("DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO", Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo>(), 32),
        ("DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2", Marshal.SizeOf<DisplayConfigGetAdvancedColorInfo2>(), 36),
        ("DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE", Marshal.SizeOf<DisplayConfigSetAdvancedColorState>(), 24),
        ("DISPLAYCONFIG_SET_HDR_STATE", Marshal.SizeOf<DisplayConfigSetHdrState>(), 24),
        ("DISPLAYCONFIG_SDR_WHITE_LEVEL", Marshal.SizeOf<DisplayConfigSdrWhiteLevel>(), 24),
    ];
}
