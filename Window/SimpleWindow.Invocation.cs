using Windows.Win32;
using Windows.Win32.Foundation;
using PhoenixTools.Window.Internal;

namespace PhoenixTools.Window;

//todo async execution

public sealed partial class SimpleWindow
{
    public void Invoke(Action action)
    {
        _ = InvokeInternal<object>(action);
    }

    public T? Invoke<T>(Func<T> func)
    {
        return InvokeInternal<T>(func);
    }

    private T? InvokeInternal<T>(Delegate method, bool synchronous = true)
    {
        var invocation = new InvocationResult(method, null, synchronous);

        _invocationQueue.Enqueue(invocation);
        PostInvokeMessage();

        invocation.AsyncWaitHandle.WaitOne();

        if (invocation is { IsCompleted: false, Exception: not null })
            throw invocation.Exception;

        return (T?)invocation.Result;
    }

    private void InvokeProc()
    {
        while (_invocationQueue.TryDequeue(out var current))
            try
            {
                var result = current.Method.DynamicInvoke(current.Args);
                current.SetCompleted(result);
            }
            catch (Exception e)
            {
                current.SetException(e);
            }
    }

    private void PostInvokeMessage()
    {
        WinApi.PostMessage((HWND)Handle, s_invokeActionMessage, 0, 0);
    }
}