using HdrSwitch.Core.Hdr;
using Xunit;

namespace HdrSwitch.Tests;

/// <summary>
/// Success is judged on the state read back from the display, not on the Win32 return code.
///
/// This is not a stylistic choice. On a Windows 11 25H2 machine with a DisplayPort monitor,
/// SET_HDR_STATE returns ERROR_ACCESS_DENIED (5) when *enabling* HDR and applies the change
/// anyway, while returning 0 when disabling. Trusting the return code reported failures that
/// had not happened and produced a non-zero exit code from a command that worked.
/// </summary>
public class HdrSetResultTests
{
    private static DisplayTarget Target(bool hdrEnabled) => new()
    {
        Index = 0,
        FriendlyName = "LS27AG55x",
        DevicePath = @"\\?\DISPLAY#SAM71E1#",
        AdapterId = 0x1234,
        TargetId = 4352,
        Capability = HdrCapability.Supported,
        HdrEnabled = hdrEnabled,
    };

    private static HdrSetResult Result(bool requested, bool actual, int win32Error) => new()
    {
        Target = Target(actual),
        Requested = requested,
        Actual = actual,
        Win32Error = win32Error,
    };

    [Fact]
    public void AccessDeniedButStateChanged_CountsAsSuccess()
    {
        var result = Result(requested: true, actual: true, win32Error: 5);

        Assert.True(result.Success);
    }

    [Fact]
    public void CleanReturnCodeButStateUnchanged_CountsAsFailure()
    {
        // The other direction matters just as much: a display on a bandwidth-limited link can
        // accept the call and refuse the change.
        var result = Result(requested: true, actual: false, win32Error: 0);

        Assert.False(result.Success);
    }

    [Fact]
    public void StateMatches_IsSuccessRegardlessOfErrorCode()
    {
        Assert.True(Result(requested: false, actual: false, win32Error: 0).Success);
        Assert.True(Result(requested: false, actual: false, win32Error: 31).Success);
    }

    [Fact]
    public void Win32ErrorIsRetainedForDiagnosis()
    {
        // Reported in `selftest` and `--json` even when the operation succeeded, so an odd
        // driver can still be identified.
        Assert.Equal(5, Result(requested: true, actual: true, win32Error: 5).Win32Error);
    }

    [Theory]
    [InlineData(HdrCapability.Supported, true)]
    [InlineData(HdrCapability.Unsupported, false)]
    [InlineData(HdrCapability.BlockedByPolicy, false)]
    public void OnlySupportedDisplaysCanToggle(HdrCapability capability, bool expected)
    {
        var target = Target(false) with { Capability = capability };

        Assert.Equal(expected, target.CanToggle);
    }

    [Theory]
    [InlineData(HdrCapability.Unsupported, false, "HDR not supported")]
    [InlineData(HdrCapability.BlockedByPolicy, false, "HDR blocked by system policy")]
    [InlineData(HdrCapability.Supported, true, "HDR on")]
    [InlineData(HdrCapability.Supported, false, "HDR off")]
    public void StatusText_DistinguishesTheThreeCapabilityStates(
        HdrCapability capability, bool enabled, string expected)
    {
        // "not supported" and "blocked by policy" must not collapse into "off": one is a
        // permanent property of the panel, the other is a condition that can change.
        var target = Target(enabled) with { Capability = capability };

        Assert.Equal(expected, target.StatusText);
    }

    [Fact]
    public void LayoutCheck_MatchesTheSdkHeaderSizes()
    {
        // A silent struct-layout mistake would produce plausible-looking garbage rather than an
        // error, so the marshalled sizes are asserted against the SDK header's implied sizes.
        var layouts = HdrController.LayoutCheck();

        Assert.NotEmpty(layouts);
        Assert.All(layouts, l => Assert.Equal(l.Expected, l.Actual));
    }

    [Fact]
    public void ForceLegacyFlag_PinsTheLegacyPathWithoutProbing()
    {
        var controller = new HdrController(forceLegacy: true);

        Assert.True(controller.LegacyForced);
        Assert.Equal(HdrApiPath.Legacy, controller.ApiPath);
    }

    [Fact]
    public void WithoutForceFlag_ApiPathIsResolvedLazily()
    {
        var controller = new HdrController(forceLegacy: false);

        Assert.False(controller.LegacyForced);
        Assert.Equal(HdrApiPath.Unknown, controller.ApiPath);
    }
}
