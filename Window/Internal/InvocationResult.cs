/* Based on System.Windows.Forms.Control.ThreadMethodEntry.cs */

namespace PhoenixTools.Window.Internal;

/// <summary>
///  Used with BeginInvoke/EndInvoke
/// </summary>
internal class InvocationResult : IAsyncResult
{
    private readonly object _lock = new();
    private readonly ManualResetEventSlim _invocationEvent = new(false);

    /// <summary>
    ///  Used with BeginInvoke/EndInvoke
    /// </summary>
    public InvocationResult(Delegate method, object?[]? args, bool synchronous)
    {
        Method = method;
        Args = args;
        Synchronous = synchronous;
    }

    ~InvocationResult()
    {
        // ReSharper disable once InconsistentlySynchronizedField
        _invocationEvent.Dispose();
    }

    public Delegate Method { get; }
    public object?[]? Args { get; }
    public bool Synchronous { get; }
    public object? AsyncState => null;

    public WaitHandle AsyncWaitHandle
    {
        get
        {
            lock (_lock)
            {
                return _invocationEvent.WaitHandle;
            }
        }
    }

    public bool CompletedSynchronously => IsCompleted && Synchronous;
    public bool IsCompleted { get; private set; }
    public Exception? Exception { get; private set; }
    public object? Result { get; private set; }

    public void SetCompleted(object? result)
    {
        lock (_lock)
        {
            Result = result;
            Exception = null;
            IsCompleted = true;
            _invocationEvent.Set();
        }
    }

    public void SetException(Exception exception)
    {
        lock (_lock)
        {
            Result = null;
            Exception = exception;
            _invocationEvent.Set();
        }
    }
}