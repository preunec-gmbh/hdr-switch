using System.Runtime.InteropServices;
using HdrSwitch.Core.Config;

namespace HdrSwitch.Ui;

/// <summary>
/// A hidden top-level window that carries the global hotkey and the display-change notifications.
///
/// It is deliberately NOT a message-only window (HWND_MESSAGE): message-only windows are excluded
/// from broadcasts, and this needs both WM_DISPLAYCHANGE and the registered "show settings"
/// broadcast that a second instance posts.
/// </summary>
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WM_DEVICECHANGE = 0x0219;
    private const int WM_SETTINGCHANGE = 0x001A;

    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const int HotkeyId = 0xB0B;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    private readonly uint _showSettingsMessage;
    private bool _hotkeyRegistered;

    internal MessageWindow()
    {
        _showSettingsMessage = RegisterWindowMessage(Program.ShowSettingsMessageName);

        CreateHandle(new CreateParams
        {
            Caption = "HdrSwitch.MessageWindow",
            Style = WS_POPUP,
            ExStyle = WS_EX_TOOLWINDOW,
            X = 0,
            Y = 0,
            Width = 0,
            Height = 0,
        });
    }

    internal event Action? HotkeyPressed;

    internal event Action? DisplayConfigurationChanged;

    internal event Action? ShowSettingsRequested;

    /// <summary>
    /// Registers the global hotkey. Returns an error string when the combination is already
    /// taken by another application, so the caller can say so instead of failing silently.
    /// </summary>
    internal string? TryRegisterHotkey(HotkeyDefinition hotkey)
    {
        UnregisterHotkey();

        if (RegisterHotKey(Handle, HotkeyId, hotkey.ModifiersForRegistration, hotkey.VirtualKey))
        {
            _hotkeyRegistered = true;
            return null;
        }

        var error = Marshal.GetLastWin32Error();
        return error == 1409 // ERROR_HOTKEY_ALREADY_REGISTERED
            ? $"{hotkey.Text} is already claimed by another application. Pick a different combination in Settings."
            : $"Could not register {hotkey.Text} (Win32 error {error}).";
    }

    internal void UnregisterHotkey()
    {
        if (!_hotkeyRegistered)
        {
            return;
        }

        UnregisterHotKey(Handle, HotkeyId);
        _hotkeyRegistered = false;
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_HOTKEY when m.WParam.ToInt32() == HotkeyId:
                HotkeyPressed?.Invoke();
                return;

            // The user may change HDR from Windows Settings or Win+Alt+B; the tray icon has to
            // stay truthful rather than showing a stale state.
            case WM_DISPLAYCHANGE:
            case WM_DEVICECHANGE:
                DisplayConfigurationChanged?.Invoke();
                break;

            case WM_SETTINGCHANGE:
                DisplayConfigurationChanged?.Invoke();
                break;

            default:
                if (_showSettingsMessage != 0 && (uint)m.Msg == _showSettingsMessage)
                {
                    ShowSettingsRequested?.Invoke();
                    return;
                }

                break;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotkey();
        if (Handle != IntPtr.Zero)
        {
            DestroyHandle();
        }
    }
}
