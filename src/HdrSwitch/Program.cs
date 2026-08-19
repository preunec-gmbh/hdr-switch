using System.Runtime.InteropServices;
using HdrSwitch.Core.Cli;
using HdrSwitch.Ui;

namespace HdrSwitch;

internal static class Program
{
    /// <summary>Local\ scope: one tray instance per user session, not per machine.</summary>
    private const string SingleInstanceMutex = @"Local\HdrSwitch.SingleInstance.9f2a1c";

    internal const string ShowSettingsMessageName = "HdrSwitch.ShowSettings";

    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static int Main(string[] args)
    {
        var options = CommandLine.Parse(args);

        if (options.IsConsoleCommand || options.Error is not null)
        {
            return ConsoleRunner.Run(options);
        }

        return RunTray();
    }

    private static int RunTray()
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutex, out var isFirstInstance);

        if (!isFirstInstance)
        {
            // Already running. Rather than exiting silently -- which reads as "the app is
            // broken" -- ask the live instance to surface its Settings window.
            var message = RegisterWindowMessage(ShowSettingsMessageName);
            if (message != 0)
            {
                PostMessage(HWND_BROADCAST, message, IntPtr.Zero, IntPtr.Zero);
            }

            return ExitCodes.Ok;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        try
        {
            using var context = new TrayApplicationContext();
            Application.Run(context);
            return ExitCodes.Ok;
        }
        finally
        {
            GC.KeepAlive(mutex);
        }
    }
}
