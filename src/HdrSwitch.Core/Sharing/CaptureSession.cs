namespace HdrSwitch.Core.Sharing;

/// <summary>Which Windows capability recorded the capture.</summary>
public enum CaptureCapability
{
    /// <summary>graphicsCaptureProgrammatic -- e.g. Discord "Go Live", app-driven capture.</summary>
    Programmatic,

    /// <summary>graphicsCaptureWithoutBorder -- capture with the yellow border suppressed.</summary>
    WithoutBorder,

    /// <summary>Detected by the coarse process fallback, not by Windows itself.</summary>
    ProcessHeuristic,
}

/// <summary>
/// One app currently capturing the screen.
///
/// <see cref="AppKey"/> deliberately keys on the executable file name rather than the full path.
/// Discord's path embeds its version (…\app-1.0.9254\Discord.exe) and changes on every update, so
/// a full-path key would silently discard the user's learned preference after each Discord patch.
/// </summary>
public sealed record CaptureSession
{
    /// <summary>Raw registry subkey name this was read from.</summary>
    public required string RegistryKey { get; init; }

    public required CaptureCapability Capability { get; init; }

    /// <summary>Decoded executable path. Null for packaged (Store) apps.</summary>
    public string? ExecutablePath { get; init; }

    /// <summary>Stable, update-proof identity used for rules. Lowercase.</summary>
    public required string AppKey { get; init; }

    /// <summary>Human-readable app name for the toast, e.g. "Discord".</summary>
    public required string AppName { get; init; }

    public required bool IsPackaged { get; init; }

    public DateTime? StartedAtUtc { get; init; }
}
