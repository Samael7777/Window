using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Diagnostics.Debug;

namespace PhoenixTools.Window.Internal;

internal static class ErrorHelper
{
    private const FORMAT_MESSAGE_OPTIONS Flags =
        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_ALLOCATE_BUFFER |
        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_FROM_SYSTEM |
        FORMAT_MESSAGE_OPTIONS.FORMAT_MESSAGE_IGNORE_INSERTS;

    /// <exception cref="Win32Exception"></exception>
    public static unsafe void ThrowLastErrorException()
    {
        Span<char> buffer = stackalloc char[512];

        var error = Marshal.GetLastWin32Error();
        var len = (int)WinApi.FormatMessage(Flags, (void*)0, (uint)error, 0, buffer, 0, null);
        var message = new string(buffer[..len]);

        throw new Win32Exception(error, message);
    }
}