using Microsoft.Win32;

namespace HdrSwitch.Core.Config;

/// <summary>
/// "Start with Windows", via the per-user Run key. HKCU rather than HKLM keeps this
/// elevation-free, which matters because the whole app is asInvoker.
/// </summary>
public static class StartupRegistration
{
    internal const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "HDR Switch";

    /// <summary>Path of the running executable. Correct for single-file publish.</summary>
    public static string ExecutablePath =>
        Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty;

    public static string ExpectedCommand => $"\"{ExecutablePath}\" --tray";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string existing && existing.Length > 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>True when startup is registered but points at a different executable (app moved).</summary>
    public static bool IsStale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string existing
                   && existing.Length > 0
                   && !string.Equals(existing, ExpectedCommand, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not open HKCU\\{RunKeyPath}.");

        if (enabled)
        {
            key.SetValue(ValueName, ExpectedCommand, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
