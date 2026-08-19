using Microsoft.Win32;

namespace HdrSwitch.Core.Hdr;

/// <summary>
/// Windows "Auto HDR" -- the DirectX feature that up-converts SDR games to HDR. It is stored as
/// a single packed string under HKCU, alongside two unrelated DirectX toggles:
///
///   HKCU\SOFTWARE\Microsoft\DirectX\UserGpuPreferences
///     DirectXUserGlobalSettings = "AutoHDREnable=1;SwapEffectUpgradeEnable=1;VRROptimizeEnable=1;"
///
/// The sibling tokens must survive a write, so this rewrites one token in place rather than
/// replacing the whole value.
/// </summary>
public static class AutoHdrSettings
{
    internal const string KeyPath = @"SOFTWARE\Microsoft\DirectX\UserGpuPreferences";
    internal const string ValueName = "DirectXUserGlobalSettings";
    internal const string AutoHdrToken = "AutoHDREnable";

    /// <summary>Null when the value or token is absent, i.e. Windows has no opinion recorded yet.</summary>
    public static bool? IsEnabled()
    {
        var raw = ReadRaw();
        if (raw is null)
        {
            return null;
        }

        var token = ReadToken(raw, AutoHdrToken);
        return token is null ? null : token == "1";
    }

    public static void SetEnabled(bool enabled)
    {
        var raw = ReadRaw() ?? string.Empty;
        var updated = WriteToken(raw, AutoHdrToken, enabled ? "1" : "0");

        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not open HKCU\\{KeyPath} for writing.");
        key.SetValue(ValueName, updated, RegistryValueKind.String);
    }

    private static string? ReadRaw()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) as string;
    }

    /// <summary>Reads one token out of a semicolon-delimited key=value string. Pure; unit tested.</summary>
    public static string? ReadToken(string packed, string token)
    {
        foreach (var part in packed.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (part.AsSpan(0, separator).Trim().Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                return part[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces one token's value, preserving every other token and its order. Appends the token
    /// when absent. Pure; unit tested.
    /// </summary>
    public static string WriteToken(string packed, string token, string value)
    {
        var parts = packed.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var replaced = false;
        for (var i = 0; i < parts.Count; i++)
        {
            var separator = parts[i].IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (parts[i].AsSpan(0, separator).Trim().Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = $"{token}={value}";
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            parts.Add($"{token}={value}");
        }

        // Windows writes this value with a trailing semicolon; match that shape exactly.
        return string.Join(';', parts) + ";";
    }
}
