using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HdrSwitch.Core.Cli;
using HdrSwitch.Core.Hdr;
using HdrSwitch.Core.Sharing;

namespace HdrSwitch;

/// <summary>
/// The command-line surface, so HDR Switch can be bound to a desktop shortcut, a Stream Deck
/// button, AutoHotkey, or a scheduled task.
///
/// This is a WinExe (no console subsystem), so output requires attaching to the parent console.
/// When there is no parent console -- launched from Explorer or a shortcut -- output-centric
/// commands fall back to a message box rather than silently producing nothing.
/// </summary>
internal static class ConsoleRunner
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    private static bool _consoleAttached;
    private static bool _redirected;
    private static readonly StringBuilder Buffer = new();

    internal static int Run(CliOptions options)
    {
        // When stdout is already redirected -- piped to a file, a script, or jq -- the handle is
        // inherited and usable as-is. Attaching to the parent console in that case would send
        // output to the wrong place, and the message-box fallback would hang a headless caller.
        _redirected = Console.IsOutputRedirected;
        _consoleAttached = !_redirected && AttachConsole(ATTACH_PARENT_PROCESS);

        try
        {
            var exitCode = Execute(options);
            Flush(options);
            return exitCode;
        }
        catch (Exception ex)
        {
            Write($"HDR Switch failed: {ex.Message}");
            Flush(options);
            return ExitCodes.Error;
        }
        finally
        {
            if (_consoleAttached)
            {
                FreeConsole();
            }
        }
    }

    private static int Execute(CliOptions options)
    {
        if (options.Error is not null)
        {
            Write(options.Error);
            Write(string.Empty);
            Write(CommandLine.Usage);
            return ExitCodes.Error;
        }

        switch (options.Command)
        {
            case CliCommand.Help:
                Write(CommandLine.Usage);
                return ExitCodes.Ok;

            case CliCommand.Version:
                Write($"HDR Switch {typeof(ConsoleRunner).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}");
                return ExitCodes.Ok;
        }

        var controller = new HdrController();
        var displays = controller.GetDisplays();

        return options.Command switch
        {
            CliCommand.List => RunList(controller, displays, options),
            CliCommand.Status => RunStatus(controller, displays, options),
            CliCommand.SelfTest => RunSelfTest(controller, displays, options),
            CliCommand.BrandCheck => RunBrandCheck(options),
            CliCommand.On => RunSet(controller, displays, options, _ => true, "on"),
            CliCommand.Off => RunSet(controller, displays, options, _ => false, "off"),
            CliCommand.Toggle => RunSet(controller, displays, options, current => !current, "toggle"),
            _ => ExitCodes.Error,
        };
    }

    private static int RunList(HdrController controller, IReadOnlyList<DisplayTarget> displays, CliOptions options)
    {
        if (options.Json)
        {
            WriteJson(BuildStatus(controller, displays), CliJsonContext.Default.StatusDto);
            return displays.Any(d => d.CanToggle) ? ExitCodes.Ok : ExitCodes.NoHdrCapableDisplay;
        }

        if (displays.Count == 0)
        {
            Write("No active displays were found.");
            return ExitCodes.Error;
        }

        Write($"CCD API path: {controller.ApiPath}{(controller.LegacyForced ? " (forced by HDRSWITCH_FORCE_LEGACY)" : string.Empty)}");
        Write(string.Empty);

        foreach (var display in displays)
        {
            Write($"  [{display.Index + 1}] {display.Label}");
            Write($"        {display.StatusText}");
            if (display.Capability != HdrCapability.Unsupported)
            {
                var wcg = display.WideColorEnabled is { } w ? $", wide colour {(w ? "on" : "off")}" : string.Empty;
                Write($"        {display.BitsPerColorChannel}-bit {display.ColorEncoding}{wcg}");
                if (display.SdrWhiteLevelNits is { } nits)
                {
                    Write($"        SDR content brightness: {nits.ToString("F0", CultureInfo.InvariantCulture)} nits");
                }
            }

            Write(string.Empty);
        }

        return displays.Any(d => d.CanToggle) ? ExitCodes.Ok : ExitCodes.NoHdrCapableDisplay;
    }

    private static int RunStatus(HdrController controller, IReadOnlyList<DisplayTarget> displays, CliOptions options)
    {
        var capable = displays.Where(d => d.CanToggle).ToList();

        if (options.Json)
        {
            WriteJson(BuildStatus(controller, displays), CliJsonContext.Default.StatusDto);
            return capable.Count > 0 ? ExitCodes.Ok : ExitCodes.NoHdrCapableDisplay;
        }

        if (capable.Count == 0)
        {
            Write("No HDR-capable display found.");
            return ExitCodes.NoHdrCapableDisplay;
        }

        foreach (var display in capable)
        {
            Write($"{display.Label}: {(display.HdrEnabled ? "HDR on" : "HDR off")}");
        }

        return ExitCodes.Ok;
    }

    private static int RunSelfTest(HdrController controller, IReadOnlyList<DisplayTarget> displays, CliOptions options)
    {
        var layouts = HdrController.LayoutCheck()
            .Select(l => new LayoutCheckDto { Struct = l.Struct, Actual = l.Actual, Expected = l.Expected })
            .ToList();

        List<string> captures;
        try
        {
            captures = ConsentStoreReader.GetActiveSessions(new RegistryProbe())
                .Select(s => $"{s.AppName} ({s.AppKey}, {s.Capability})")
                .ToList();
        }
        catch (Exception ex)
        {
            captures = [$"capture probe failed: {ex.Message}"];
        }

        var dto = new SelfTestDto
        {
            ApiPath = controller.ApiPath.ToString(),
            LegacyForced = controller.LegacyForced,
            OsVersion = Environment.OSVersion.VersionString,
            StructLayouts = layouts,
            LayoutsOk = layouts.All(l => l.Ok),
            Displays = displays.Select(DisplayDto.From).ToList(),
            ActiveCaptures = captures,
        };

        if (options.Json)
        {
            WriteJson(dto, CliJsonContext.Default.SelfTestDto);
            return dto.LayoutsOk ? ExitCodes.Ok : ExitCodes.Error;
        }

        Write($"OS                : {dto.OsVersion}");
        Write($"CCD API path      : {dto.ApiPath}{(dto.LegacyForced ? " (forced)" : " (probed)")}");
        Write(string.Empty);

        Write("Interop struct layouts (marshalled vs SDK header):");
        foreach (var layout in layouts)
        {
            var mark = layout.Ok ? "ok  " : "FAIL";
            Write($"  {mark} {layout.Struct,-42} {layout.Actual,4} (expected {layout.Expected})");
        }

        Write(string.Empty);
        Write($"Displays ({displays.Count}):");
        foreach (var display in displays)
        {
            Write($"  [{display.Index + 1}] {display.Label,-28} {display.StatusText,-32} flags=0x{display.RawFlags:X8}");
        }

        Write(string.Empty);
        Write(captures.Count == 0 ? "Active screen captures: none" : "Active screen captures:");
        foreach (var capture in captures)
        {
            Write($"  {capture}");
        }

        if (!dto.LayoutsOk)
        {
            Write(string.Empty);
            Write("A struct layout does not match the SDK header. HDR reads would be unreliable.");
            return ExitCodes.Error;
        }

        return ExitCodes.Ok;
    }

    private static int RunBrandCheck(CliOptions options)
    {
        Write($"Fonts resolved    : {Ui.Brand.FontReport}");
        Write($"Theme polarity    : {(Ui.Brand.IsDark ? "dark" : "light")}");
        Write($"Wordmark min width: {Ui.Wordmark.MinimumWidthPx} px (below this it is not drawn)");
        Write(string.Empty);

        try
        {
            var path = Ui.BrandPreview.Render(options.OutPath);
            Write($"Preview written to: {path}");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                foreach (var file in Ui.BrandPreview.RenderSettingsTabs(directory))
                {
                    Write($"  settings tab   : {file}");
                }

                foreach (var file in Ui.BrandPreview.RenderToasts(directory))
                {
                    Write($"  toast          : {file}");
                }
            }

            return ExitCodes.Ok;
        }
        catch (Exception ex)
        {
            Write($"Could not render the brand preview: {ex.Message}");
            return ExitCodes.Error;
        }
    }

    private static int RunSet(
        HdrController controller,
        IReadOnlyList<DisplayTarget> displays,
        CliOptions options,
        Func<bool, bool> desiredFromCurrent,
        string actionName)
    {
        if (!DisplaySelection.TryResolve(displays, options.DisplaySelector, out var matched, out var error))
        {
            Write(error ?? "Could not resolve the requested display.");
            return ExitCodes.Error;
        }

        var actionable = matched.Where(d => d.CanToggle).ToList();
        if (actionable.Count == 0)
        {
            var blocked = matched.Where(d => d.Capability == HdrCapability.BlockedByPolicy).ToList();
            Write(blocked.Count > 0
                ? $"HDR is blocked by system policy on: {string.Join(", ", blocked.Select(d => d.Label))}"
                : "No HDR-capable display matched.");
            return ExitCodes.NoHdrCapableDisplay;
        }

        // "toggle" across several displays uses the first display's state as the reference, so a
        // mixed set converges instead of flipping each display in a different direction.
        var reference = actionable[0].HdrEnabled;
        var desired = desiredFromCurrent(reference);

        var results = actionable.Select(d => controller.SetHdr(d, desired)).ToList();
        var success = results.All(r => r.Success);

        if (options.Json)
        {
            WriteJson(new ActionResultDto
            {
                Action = actionName,
                ApiPath = controller.ApiPath.ToString(),
                Success = success,
                Results = results.Select(ActionItemDto.From).ToList(),
            }, CliJsonContext.Default.ActionResultDto);

            return success ? ExitCodes.Ok : ExitCodes.Error;
        }

        foreach (var result in results)
        {
            Write(result.Success
                ? $"{result.Target.Label}: HDR {(result.Actual ? "on" : "off")} ({result.SettleMilliseconds} ms)"
                : $"{result.Target.Label}: {result.Message ?? "failed"}");
        }

        return success ? ExitCodes.Ok : ExitCodes.Error;
    }

    private static StatusDto BuildStatus(HdrController controller, IReadOnlyList<DisplayTarget> displays) => new()
    {
        ApiPath = controller.ApiPath.ToString(),
        AnyHdrEnabled = displays.Any(d => d.HdrEnabled),
        Displays = displays.Select(DisplayDto.From).ToList(),
    };

    private static void WriteJson<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        Write(JsonSerializer.Serialize(value, typeInfo));

    private static void Write(string line) => Buffer.AppendLine(line);

    private static void Flush(CliOptions options)
    {
        if (options.Quiet || Buffer.Length == 0)
        {
            return;
        }

        var text = Buffer.ToString().TrimEnd();

        if (_redirected || _consoleAttached)
        {
            Console.Out.WriteLine(text);
            Console.Out.Flush();
            return;
        }

        // No console at all (launched from Explorer or a shortcut). Showing nothing would look
        // like the app failed to start -- but never block a non-interactive caller on a dialog.
        if (!Environment.UserInteractive)
        {
            return;
        }

        System.Windows.Forms.MessageBox.Show(
            text,
            "HDR Switch",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information);
    }
}
