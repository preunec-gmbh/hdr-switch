using System.Runtime.InteropServices;

namespace HdrSwitch.Core.Interop;

/// <summary>
/// P/Invoke surface for the Windows CCD (Connecting and Configuring Displays) API.
///
/// Every constant and struct layout in this file was transcribed from the installed
/// Windows SDK header rather than from memory:
///   C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um\wingdi.h
/// The device-info type values are especially easy to get wrong: SET_HDR_STATE is 16,
/// not 17, because GET_SDR_WHITE_LEVEL occupies 11 and shifts everything after it.
/// </summary>
internal static class DisplayConfigNative
{
    // --- QueryDisplayConfig flags (wingdi.h:3330-3335) ---
    internal const uint QDC_ALL_PATHS = 0x00000001;
    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    internal const uint QDC_VIRTUAL_MODE_AWARE = 0x00000010;

    // --- Win32 error codes ---
    internal const int ERROR_SUCCESS = 0;
    internal const int ERROR_ACCESS_DENIED = 5;
    internal const int ERROR_GEN_FAILURE = 31;
    internal const int ERROR_NOT_SUPPORTED = 50;
    internal const int ERROR_INVALID_PARAMETER = 87;
    internal const int ERROR_INSUFFICIENT_BUFFER = 122;

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName packet);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetAdvancedColorInfo packet);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigGetAdvancedColorInfo2 packet);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSdrWhiteLevel packet);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int DisplayConfigSetDeviceInfo(ref DisplayConfigSetAdvancedColorState packet);

    [DllImport("user32.dll", ExactSpelling = true)]
    internal static extern int DisplayConfigSetDeviceInfo(ref DisplayConfigSetHdrState packet);
}

/// <summary>DISPLAYCONFIG_DEVICE_INFO_TYPE (wingdi.h:3033-3051).</summary>
internal enum DisplayConfigDeviceInfoType : uint
{
    GetSourceName = 1,
    GetTargetName = 2,
    GetTargetPreferredMode = 3,
    GetAdapterName = 4,
    GetTargetBaseType = 6,
    GetAdvancedColorInfo = 9,
    SetAdvancedColorState = 10,
    GetSdrWhiteLevel = 11,
    GetMonitorSpecialization = 12,
    SetMonitorSpecialization = 13,
    SetReserved1 = 14,
    GetAdvancedColorInfo2 = 15,
    SetHdrState = 16,
    SetWcgState = 17,
}

[StructLayout(LayoutKind.Sequential)]
internal struct Luid
{
    public uint LowPart;
    public int HighPart;

    public long ToInt64() => ((long)HighPart << 32) | LowPart;

    public static Luid FromInt64(long value) => new()
    {
        LowPart = unchecked((uint)(value & 0xFFFFFFFF)),
        HighPart = (int)(value >> 32),
    };
}

/// <summary>DISPLAYCONFIG_DEVICE_INFO_HEADER -- 20 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigDeviceInfoHeader
{
    public DisplayConfigDeviceInfoType Type;
    public uint Size;
    public Luid AdapterId;
    public uint Id;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigRational
{
    public uint Numerator;
    public uint Denominator;
}

/// <summary>DISPLAYCONFIG_PATH_SOURCE_INFO -- 20 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathSourceInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIdx;
    public uint StatusFlags;
}

/// <summary>DISPLAYCONFIG_PATH_TARGET_INFO -- 48 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathTargetInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIdx;
    public uint OutputTechnology;
    public uint Rotation;
    public uint Scaling;
    public DisplayConfigRational RefreshRate;
    public uint ScanLineOrdering;
    [MarshalAs(UnmanagedType.Bool)] public bool TargetAvailable;
    public uint StatusFlags;
}

/// <summary>DISPLAYCONFIG_PATH_INFO -- 72 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo SourceInfo;
    public DisplayConfigPathTargetInfo TargetInfo;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfig2DRegion
{
    public uint Cx;
    public uint Cy;
}

/// <summary>DISPLAYCONFIG_VIDEO_SIGNAL_INFO -- 48 bytes, the largest arm of the mode union.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigVideoSignalInfo
{
    public ulong PixelRate;
    public DisplayConfigRational HSyncFreq;
    public DisplayConfigRational VSyncFreq;
    public DisplayConfig2DRegion ActiveSize;
    public DisplayConfig2DRegion TotalSize;
    public uint VideoStandard;
    public uint ScanLineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PointL
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSourceMode
{
    public uint Width;
    public uint Height;
    public uint PixelFormat;
    public PointL Position;
}

[StructLayout(LayoutKind.Explicit)]
internal struct DisplayConfigModeInfoUnion
{
    [FieldOffset(0)] public DisplayConfigVideoSignalInfo TargetMode;
    [FieldOffset(0)] public DisplayConfigSourceMode SourceMode;
}

/// <summary>DISPLAYCONFIG_MODE_INFO -- 64 bytes. Contents unused; we only need a correctly sized buffer.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigModeInfo
{
    public uint InfoType;
    public uint Id;
    public Luid AdapterId;
    public DisplayConfigModeInfoUnion ModeInfo;
}

/// <summary>DISPLAYCONFIG_TARGET_DEVICE_NAME -- 420 bytes.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayConfigTargetDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Flags;
    public uint OutputTechnology;
    public ushort EdidManufactureId;
    public ushort EdidProductCodeId;
    public uint ConnectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string MonitorFriendlyDeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorDevicePath;
}

/// <summary>
/// DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO (wingdi.h:3167-3186) -- the legacy read.
/// Bitfield: 0 supported, 1 enabled, 2 wideColorEnforced, 3 forceDisabled.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigGetAdvancedColorInfo
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Value;
    public uint ColorEncoding;
    public uint BitsPerColorChannel;

    public bool AdvancedColorSupported => (Value & 0x1) != 0;
    public bool AdvancedColorEnabled => (Value & 0x2) != 0;
    public bool WideColorEnforced => (Value & 0x4) != 0;
    public bool AdvancedColorForceDisabled => (Value & 0x8) != 0;
}

/// <summary>
/// DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 (wingdi.h:3212-3241) -- Win11 24H2+.
/// Separates true HDR from wide-colour gamut, which the legacy struct conflates.
/// Bitfield: 0 acSupported, 1 acActive, 2 reserved, 3 acLimitedByPolicy,
///           4 hdrSupported, 5 hdrUserEnabled, 6 wcgSupported, 7 wcgUserEnabled.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigGetAdvancedColorInfo2
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Value;
    public uint ColorEncoding;
    public uint BitsPerColorChannel;
    public uint ActiveColorMode;

    public bool AdvancedColorSupported => (Value & 0x01) != 0;
    public bool AdvancedColorActive => (Value & 0x02) != 0;
    public bool AdvancedColorLimitedByPolicy => (Value & 0x08) != 0;
    public bool HighDynamicRangeSupported => (Value & 0x10) != 0;
    public bool HighDynamicRangeUserEnabled => (Value & 0x20) != 0;
    public bool WideColorSupported => (Value & 0x40) != 0;
    public bool WideColorUserEnabled => (Value & 0x80) != 0;
}

/// <summary>DISPLAYCONFIG_ADVANCED_COLOR_MODE (wingdi.h:3205-3210).</summary>
internal enum AdvancedColorMode : uint
{
    Sdr = 0,
    Wcg = 1,
    Hdr = 2,
}

/// <summary>DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE -- the legacy write.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSetAdvancedColorState
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Value;
}

/// <summary>DISPLAYCONFIG_SET_HDR_STATE -- the modern write (Win11 24H2+).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSetHdrState
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Value;
}

/// <summary>
/// DISPLAYCONFIG_SDR_WHITE_LEVEL (wingdi.h:3276-3285). Read-only here: there is no documented
/// setter, so HDR Switch reports this value but deliberately does not offer a slider.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSdrWhiteLevel
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint SdrWhiteLevel;

    /// <summary>Per the header comment: SDRWhiteLevel in nits = (SDRWhiteLevel / 1000) * 80.</summary>
    public double Nits => SdrWhiteLevel / 1000.0 * 80.0;
}
