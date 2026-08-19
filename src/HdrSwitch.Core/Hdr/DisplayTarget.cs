namespace HdrSwitch.Core.Hdr;

/// <summary>Which CCD API generation the controller resolved to at startup.</summary>
public enum HdrApiPath
{
    Unknown = 0,

    /// <summary>GET_ADVANCED_COLOR_INFO_2 (15) + SET_HDR_STATE (16). Windows 11 24H2 and later.</summary>
    Modern = 1,

    /// <summary>GET_ADVANCED_COLOR_INFO (9) + SET_ADVANCED_COLOR_STATE (10). Windows 10 1803+.</summary>
    Legacy = 2,
}

/// <summary>How a display relates to HDR, as three genuinely different states.</summary>
public enum HdrCapability
{
    /// <summary>The panel or link cannot do HDR at all. Nothing to toggle.</summary>
    Unsupported = 0,

    /// <summary>HDR is available and togglable.</summary>
    Supported = 1,

    /// <summary>
    /// The panel reports support but the OS or driver is blocking it -- for example a
    /// bandwidth-limited link, or advancedColorForceDisabled. Toggling will not stick.
    /// </summary>
    BlockedByPolicy = 2,
}

/// <summary>
/// One active display path with its HDR state.
///
/// Identity note: <see cref="AdapterId"/> is a LUID and is NOT stable across reboots or
/// GPU driver restarts. Anything persisted -- rules, per-display preferences -- must key on
/// <see cref="StableId"/> instead, which is derived from the monitor device path.
/// </summary>
public sealed record DisplayTarget
{
    /// <summary>Zero-based index in the active path list. Stable only within one enumeration.</summary>
    public required int Index { get; init; }

    /// <summary>e.g. "Samsung U28E590". May be empty for some virtual or generic panels.</summary>
    public required string FriendlyName { get; init; }

    /// <summary>The monitor device interface path. Stable across reboots; used as identity.</summary>
    public required string DevicePath { get; init; }

    public required long AdapterId { get; init; }
    public required uint TargetId { get; init; }

    public required HdrCapability Capability { get; init; }

    /// <summary>True when HDR is currently switched on for this display.</summary>
    public required bool HdrEnabled { get; init; }

    /// <summary>Wide colour gamut, reported separately by the modern API. Null on the legacy path.</summary>
    public bool? WideColorEnabled { get; init; }

    public uint BitsPerColorChannel { get; init; }
    public string ColorEncoding { get; init; } = "unknown";

    /// <summary>SDR content brightness, in nits. Only meaningful while HDR is on.</summary>
    public double? SdrWhiteLevelNits { get; init; }

    /// <summary>Raw flag word from the CCD query, surfaced by `selftest` for diagnosis.</summary>
    public uint RawFlags { get; init; }

    public bool CanToggle => Capability == HdrCapability.Supported;

    /// <summary>
    /// Reboot-stable identity. Falls back to the friendly name, then the target id, when a
    /// device path is unavailable -- degrading rather than colliding on an empty string.
    /// </summary>
    public string StableId =>
        !string.IsNullOrWhiteSpace(DevicePath) ? DevicePath
        : !string.IsNullOrWhiteSpace(FriendlyName) ? $"name:{FriendlyName}"
        : $"target:{AdapterId:X}:{TargetId}";

    /// <summary>Name for menus and CLI output, never empty.</summary>
    public string Label =>
        !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName : $"Display {Index + 1}";

    public string StatusText => Capability switch
    {
        HdrCapability.Unsupported => "HDR not supported",
        HdrCapability.BlockedByPolicy => "HDR blocked by system policy",
        _ => HdrEnabled ? "HDR on" : "HDR off",
    };
}

/// <summary>Result of attempting to change HDR on one display.</summary>
public sealed record HdrSetResult
{
    public required DisplayTarget Target { get; init; }
    public required bool Requested { get; init; }

    /// <summary>State read back after the call. The API can return success and still not stick.</summary>
    public required bool Actual { get; init; }

    /// <summary>Informational only. Some drivers return an error and apply the change anyway.</summary>
    public required int Win32Error { get; init; }

    /// <summary>How long the change took to become readable.</summary>
    public int SettleMilliseconds { get; init; }

    /// <summary>False when the display never reported the requested state within the budget.</summary>
    public bool Settled { get; init; }

    public string? Message { get; init; }

    /// <summary>
    /// Judged purely on the state read back afterwards. The Win32 return code is not reliable:
    /// SET_HDR_STATE has been observed returning ERROR_ACCESS_DENIED while succeeding.
    /// </summary>
    public bool Success => Actual == Requested;
}
