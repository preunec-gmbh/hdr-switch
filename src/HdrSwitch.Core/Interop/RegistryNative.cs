using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HdrSwitch.Core.Interop;

/// <summary>
/// RegNotifyChangeKeyValue lets us react to a screen capture starting within milliseconds
/// instead of polling. The notification is one-shot: it must be re-armed after every signal.
/// </summary>
internal static class RegistryNative
{
    internal const int REG_NOTIFY_CHANGE_NAME = 0x00000001;
    internal const int REG_NOTIFY_CHANGE_ATTRIBUTES = 0x00000002;
    internal const int REG_NOTIFY_CHANGE_LAST_SET = 0x00000004;
    internal const int REG_NOTIFY_CHANGE_SECURITY = 0x00000008;

    /// <summary>
    /// Without this the notification is cancelled when the registering thread exits, which is a
    /// classic source of a watcher that silently stops working after a while.
    /// </summary>
    internal const int REG_NOTIFY_THREAD_AGNOSTIC = 0x10000000;

    [DllImport("advapi32.dll", SetLastError = true)]
    internal static extern int RegNotifyChangeKeyValue(
        SafeRegistryHandle hKey,
        [MarshalAs(UnmanagedType.Bool)] bool watchSubtree,
        int notifyFilter,
        SafeWaitHandle hEvent,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
}
