namespace PhoenixTools.Window;

public class WindowDestroyedEventArgs : EventArgs
{
    public nint OldHandle { get; }
    public int OldThreadId { get; }

    public WindowDestroyedEventArgs(nint oldHandle, int oldThreadId)
    {
        OldHandle = oldHandle;
        OldThreadId = oldThreadId;
    }
}