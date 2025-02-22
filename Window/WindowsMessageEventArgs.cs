namespace PhoenixTools.Window;

public class WindowsMessageEventArgs(nint hWnd, uint msg, nuint wParam, nint lParam)
    : EventArgs
{
    public nint WindowHandle { get; } = hWnd;
    public uint Message { get; } = msg;
    public nuint WParam { get; } = wParam;
    public nint LParam { get; } = lParam;
}