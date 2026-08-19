namespace HdrSwitch.Core.Cli;

public enum CliCommand
{
    /// <summary>No arguments: run the tray application.</summary>
    Tray,
    On,
    Off,
    Toggle,
    Status,
    List,
    SelfTest,
    BrandCheck,
    Help,
    Version,
}

/// <summary>Process exit codes. Documented because shortcuts and scripts depend on them.</summary>
public static class ExitCodes
{
    public const int Ok = 0;
    public const int Error = 1;
    public const int NoHdrCapableDisplay = 2;
}

public sealed record CliOptions
{
    public required CliCommand Command { get; init; }

    /// <summary>Display index (1-based) or a name fragment. Null means "all capable displays".</summary>
    public string? DisplaySelector { get; init; }

    public bool Json { get; init; }

    public bool Quiet { get; init; }

    /// <summary>Output file for commands that write one (brandcheck).</summary>
    public string? OutPath { get; init; }

    /// <summary>Set when parsing failed; the caller should print this and exit non-zero.</summary>
    public string? Error { get; init; }

    public bool IsConsoleCommand => Command is not CliCommand.Tray;
}

/// <summary>Argument parsing for the CLI surface. Pure; unit tested.</summary>
public static class CommandLine
{
    public const string Usage = """
        HDR Switch -- toggle Windows HDR from the tray, a hotkey, or a shortcut.

        Usage:
          HdrSwitch.exe                     Run the tray application.
          HdrSwitch.exe on   [options]      Turn HDR on.
          HdrSwitch.exe off  [options]      Turn HDR off.
          HdrSwitch.exe toggle [options]    Flip HDR.
          HdrSwitch.exe status [options]    Show current HDR state.
          HdrSwitch.exe list [options]      List displays and their HDR capability.
          HdrSwitch.exe selftest            Diagnose the CCD API path and struct layouts.
          HdrSwitch.exe brandcheck          Render the vendored brand assets to a PNG for review.

        Options:
          --out <file>         brandcheck only: where to write the preview PNG.
          --display <n|name>   Act on one display: 1-based index, or part of its name.
                               Default is every HDR-capable display.
          --all                Explicitly target all displays (the default).
          --json               Machine-readable output.
          --quiet              Suppress output; rely on the exit code.
          -h, --help           Show this help.
          --version            Show the version.

        Exit codes:
          0  success
          1  error
          2  no HDR-capable display found

        Environment:
          HDRSWITCH_FORCE_LEGACY=1   Force the pre-24H2 CCD API path (for testing the fallback).
        """;

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CliOptions { Command = CliCommand.Tray };
        }

        CliCommand? command = null;
        string? displaySelector = null;
        var json = false;
        var quiet = false;
        var all = false;
        string? outPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "--tray":
                    command ??= CliCommand.Tray;
                    continue;
                case "on" or "enable":
                    if (TrySetCommand(ref command, CliCommand.On, arg, out var e1)) continue; else return e1!;
                case "off" or "disable":
                    if (TrySetCommand(ref command, CliCommand.Off, arg, out var e2)) continue; else return e2!;
                case "toggle":
                    if (TrySetCommand(ref command, CliCommand.Toggle, arg, out var e3)) continue; else return e3!;
                case "status":
                    if (TrySetCommand(ref command, CliCommand.Status, arg, out var e4)) continue; else return e4!;
                case "list" or "displays":
                    if (TrySetCommand(ref command, CliCommand.List, arg, out var e5)) continue; else return e5!;
                case "selftest" or "self-test":
                    if (TrySetCommand(ref command, CliCommand.SelfTest, arg, out var e6)) continue; else return e6!;
                case "brandcheck" or "brand-check":
                    if (TrySetCommand(ref command, CliCommand.BrandCheck, arg, out var e7)) continue; else return e7!;
                case "-h" or "--help" or "help" or "/?":
                    return new CliOptions { Command = CliCommand.Help };
                case "--version" or "-v":
                    return new CliOptions { Command = CliCommand.Version };
                case "--json":
                    json = true;
                    continue;
                case "--quiet" or "-q":
                    quiet = true;
                    continue;
                case "--all":
                    all = true;
                    continue;
                case "--out":
                    if (i + 1 >= args.Length)
                    {
                        return Fail("--out needs a file path.");
                    }

                    outPath = args[++i];
                    continue;
                case "--display" or "-d":
                    if (i + 1 >= args.Length)
                    {
                        return Fail("--display needs a value: an index like 1, or part of a display name.");
                    }

                    displaySelector = args[++i];
                    continue;
            }

            if (arg.StartsWith("--display=", StringComparison.OrdinalIgnoreCase))
            {
                displaySelector = arg["--display=".Length..];
                continue;
            }

            return Fail($"Unrecognised argument '{arg}'. Run HdrSwitch.exe --help for usage.");
        }

        if (all && displaySelector is not null)
        {
            return Fail("--all and --display contradict each other; use one or the other.");
        }

        return new CliOptions
        {
            Command = command ?? CliCommand.Tray,
            DisplaySelector = displaySelector,
            Json = json,
            Quiet = quiet,
            OutPath = outPath,
        };
    }

    private static bool TrySetCommand(ref CliCommand? slot, CliCommand value, string token, out CliOptions? error)
    {
        if (slot is not null && slot != value && slot != CliCommand.Tray)
        {
            error = Fail($"'{token}' conflicts with the earlier command '{slot.ToString()!.ToLowerInvariant()}'.");
            return false;
        }

        slot = value;
        error = null;
        return true;
    }

    private static CliOptions Fail(string message) => new()
    {
        Command = CliCommand.Help,
        Error = message,
    };
}
