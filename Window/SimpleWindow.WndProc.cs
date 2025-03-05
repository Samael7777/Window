using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PhoenixTools.Window;

public sealed partial class SimpleWindow
{
    private int WndProc(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        var maskedMessage = message & 0xFFFF;
        if (maskedMessage == (uint)WindowsMessage.WM_DESTROY)
        {
            WinApi.PostQuitMessage(0);
            return 1;
        }

        if (message == s_invokeActionMessage)
        {
            InvokeProc();
            return 1;
        }

        var args = new WindowsMessageEventArgs(hWnd, message, wParam, lParam);
        MessageReceived?.Invoke(this, args);

        return args.IsHandled ? 1 : 0;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static LRESULT DefaultWndProc(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (s_wndProcDelegates.TryGetValue(hWnd, out var wndProc))
            if (wndProc(hWnd, message, wParam, lParam) == 1)
                return (LRESULT)1;

        return WinApi.DefWindowProc(hWnd, message, wParam, lParam);
    }
}