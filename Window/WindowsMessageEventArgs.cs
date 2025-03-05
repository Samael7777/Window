namespace PhoenixTools.Window;

public class WindowsMessageEventArgs : EventArgs
{
    private readonly object _lock = new();
    private bool _isHandled;

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

    public bool IsHandled
    {
        get
        {
            lock (_lock)
            {
                return _isHandled;
            }
        }
        set
        {
            lock (_lock)
            {
                if (_isHandled) return;

                _isHandled = value;
            }
        }
    }
}