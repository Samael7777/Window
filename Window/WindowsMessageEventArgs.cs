namespace PhoenixTools.Window;

public class WindowsMessageEventArgs : EventArgs
{
    public WindowsMessageEventArgs(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        WindowHandle = hWnd;
        Message = msg;
        WParam = wParam;
        LParam = lParam;
    }

    public nint WindowHandle { get; }
    public uint Message { get; }
    public nuint WParam { get; }
    public nint LParam { get; }
}