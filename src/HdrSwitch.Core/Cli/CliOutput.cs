using System.Text.Json.Serialization;
using HdrSwitch.Core.Hdr;

namespace HdrSwitch.Core.Cli;

public sealed record DisplayDto
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string DevicePath { get; init; }
    public required string Capability { get; init; }
    public required bool HdrEnabled { get; init; }
    public bool? WideColorEnabled { get; init; }
    public uint BitsPerColorChannel { get; init; }
    public string ColorEncoding { get; init; } = string.Empty;
    public double? SdrWhiteLevelNits { get; init; }
    public string RawFlags { get; init; } = string.Empty;

    public static DisplayDto From(DisplayTarget target) => new()
    {
        Index = target.Index + 1,
        Name = target.Label,
        DevicePath = target.DevicePath,
        Capability = target.Capability.ToString(),
        HdrEnabled = target.HdrEnabled,
        WideColorEnabled = target.WideColorEnabled,
        BitsPerColorChannel = target.BitsPerColorChannel,
        ColorEncoding = target.ColorEncoding,
        SdrWhiteLevelNits = target.SdrWhiteLevelNits,
        RawFlags = $"0x{target.RawFlags:X8}",
    };
}

public sealed record StatusDto
{
    public required string ApiPath { get; init; }
    public required bool AnyHdrEnabled { get; init; }
    public required IReadOnlyList<DisplayDto> Displays { get; init; }
}

public sealed record ActionItemDto
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required bool Requested { get; init; }
    public required bool Actual { get; init; }
    public required bool Success { get; init; }
    public required int Win32Error { get; init; }
    public int SettleMilliseconds { get; init; }
    public string? Message { get; init; }

    public static ActionItemDto From(HdrSetResult result) => new()
    {
        Index = result.Target.Index + 1,
        Name = result.Target.Label,
        Requested = result.Requested,
        Actual = result.Actual,
        Success = result.Success,
        Win32Error = result.Win32Error,
        SettleMilliseconds = result.SettleMilliseconds,
        Message = result.Message,
    };
}

public sealed record ActionResultDto
{
    public required string Action { get; init; }
    public required string ApiPath { get; init; }
    public required bool Success { get; init; }
    public required IReadOnlyList<ActionItemDto> Results { get; init; }
}

public sealed record SelfTestDto
{
    public required string ApiPath { get; init; }
    public required bool LegacyForced { get; init; }
    public required string OsVersion { get; init; }
    public required IReadOnlyList<LayoutCheckDto> StructLayouts { get; init; }
    public required bool LayoutsOk { get; init; }
    public required IReadOnlyList<DisplayDto> Displays { get; init; }
    public required IReadOnlyList<string> ActiveCaptures { get; init; }
}

public sealed record LayoutCheckDto
{
    public required string Struct { get; init; }
    public required int Actual { get; init; }
    public required int Expected { get; init; }
    public bool Ok => Actual == Expected;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StatusDto))]
[JsonSerializable(typeof(ActionResultDto))]
[JsonSerializable(typeof(SelfTestDto))]
public partial class CliJsonContext : JsonSerializerContext;
