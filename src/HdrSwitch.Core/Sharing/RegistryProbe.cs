using System.Security;
using Microsoft.Win32;

namespace HdrSwitch.Core.Sharing;

/// <summary>Live HKCU implementation of <see cref="IRegistryProbe"/>.</summary>
public sealed class RegistryProbe : IRegistryProbe
{
    public IReadOnlyList<string> GetSubKeyNames(string hkcuPath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(hkcuPath);
            return key?.GetSubKeyNames() ?? [];
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            // A missing or unreadable key means "nothing is capturing", not a crash.
            return [];
        }
    }

    public long? GetQwordValue(string hkcuPath, string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(hkcuPath);
            return key?.GetValue(valueName) switch
            {
                long l => l,
                int i => i,
                _ => null,
            };
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
