using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using PhoenixTools.Window.Internal;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace PhoenixTools.Window;

//todo async execution

public sealed class SimpleWindow : IDisposable
{
    private const string InvokeActionMessageName = "InvokeActionMessage{8FD8734C-9B9D-4866-944B-54B81B9E3D7C}";
    private const string WndNamePrefix = "PhoenixToolsWindow";
    
    private delegate void WndProcDelegate(HWND wnd, uint message, WPARAM wParam, LPARAM lParam);

    private static readonly HWND HWND_MESSAGE = (HWND)(IntPtr)(-3);
    private static readonly uint InvokeActionMessage = WinApi.RegisterWindowMessage(InvokeActionMessageName);
    
    private static WndProcDelegate? _wndProcDelegate;

    private readonly Queue<InvocationResult> _invocationQueue = new();
    private readonly WNDCLASSEXW _windowClass;
    private readonly string _windowClassName;
    private readonly Task _captureTask;
    
    private HWND _captureWndHandle;
    
    public event EventHandler? WindowCreated;
    public event EventHandler? WindowDestroyed;
    public event EventHandler<WindowsMessageEventArgs>? MessageReceived;

    public nint Handle => _captureWndHandle;
    public int ThreadId { get; private set; }
    public string Name { get; }

    public SimpleWindow(bool isMessageOnly = true)
    {
        BuildWindowsName(isMessageOnly, out _windowClassName, out var wndName);
        Name = wndName;
       
        _windowClass = BuildWindowClass(_windowClassName);
        var classHandle = WinApi.RegisterClassEx(_windowClass);
        if (classHandle == 0)
            throw ErrorHelper.GetLastWin32Exception();

        _captureTask = StartCaptureTask(wndName, isMessageOnly);

        if (_captureWndHandle.IsNull)
            throw ErrorHelper.GetLastWin32Exception();
    }

    #pragma warning disable CS8774
    [MemberNotNull(nameof(_captureWndHandle))]
    private Task StartCaptureTask(string wndName, bool isMessageOnly)
    {
        var waitInitEvent = new ManualResetEventSlim(false);

        var task = new Task(()=>CaptureTask(wndName, isMessageOnly, waitInitEvent), 
            TaskCreationOptions.LongRunning
            | TaskCreationOptions.DenyChildAttach
            | TaskCreationOptions.HideScheduler);
        task.Start();

        waitInitEvent.Wait();

        return task;
    }
    #pragma warning restore CS8774

    public void Invoke(Action action)
    {
        _ = InvokeInternal(action, null, true);
    }

    public T? Invoke<T>(Func<T> func)
    {
        return (T?)InvokeInternal(func, null, true);
    }

    private object? InvokeInternal(Delegate method, object?[]? args, bool syncronus)
    {
        var currentThreadId = WinApi.GetCurrentThreadId();
        if (currentThreadId == ThreadId) return method.DynamicInvoke(args);
        
        var invocation = new InvocationResult(method, null, syncronus);
        lock (_invocationQueue)
        {
            _invocationQueue.Enqueue(invocation);
        }
        PostInvokeMessage();
        
        invocation.AsyncWaitHandle.WaitOne();
        
        if (invocation is { IsCompleted: false, Exception: not null }) 
            throw invocation.Exception;

        return invocation.Result;
    }
    
    [MemberNotNull(nameof(_captureWndHandle))]
    private void CaptureTask(string windowName, bool isMessageOnly, ManualResetEventSlim initEvent)
    {
        ThreadId = (int)WinApi.GetCurrentThreadId();
        _captureWndHandle = CreateWindow(_windowClassName, _windowClass, windowName, isMessageOnly);
        _wndProcDelegate += WndProc;

        initEvent.Set(); //Capture window created. Unlock methods.

		WindowCreated?.Invoke(this, EventArgs.Empty);

        int error;
        while ((error = GetMessage(out var message)) != 0)
        {
            if (error == -1)
            {
                //Handle the error

            }
            else
            {
                WinApi.TranslateMessage(in message);
                _ = WinApi.DispatchMessage(in message);
            }
        }

        _wndProcDelegate -= WndProc;
        ThreadId = -1;
        WindowDestroyed?.Invoke(this, EventArgs.Empty);
    }

    private void OnMessageReceived(HWND wnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        var args = new WindowsMessageEventArgs(wnd, message, wParam, lParam);
        MessageReceived?.Invoke(this, args);
    }

    private void InvokeProc()
    {
        lock (_invocationQueue)
        {
            if (_invocationQueue.Count == 0) return;
            while (_invocationQueue.TryDequeue(out var current))
            {
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
        }
    }

    private void PostInvokeMessage()
    {
        WinApi.PostMessage((HWND)Handle, InvokeActionMessage, 0, 0);
    }

    private void WndProc(HWND wnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == InvokeActionMessage)
            InvokeProc();
        else
            OnMessageReceived(wnd, message, wParam.Value, lParam);
    }

    private void CloseWindow()
    {
        Invoke(() => WinApi.DestroyWindow(_captureWndHandle));
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static LRESULT DefaultWndProc(HWND wnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        var maskedMessage = message & 0xFFFF;
       
        Debug.WriteLine(Enum.GetName(typeof(WindowsMessage), message));

        switch (maskedMessage)
        {
            case (uint)WindowsMessage.WM_DESTROY:
            case (uint)WindowsMessage.WM_QUIT:
                WinApi.PostQuitMessage(0);
                return (LRESULT)0;
            default:
                _wndProcDelegate?.Invoke(wnd, message, wParam, lParam);
                break;
        }
        return WinApi.DefWindowProc(wnd, message, wParam, lParam);
    }
    
    private static unsafe WNDCLASSEXW BuildWindowClass(string wndClassName)
    {
        var currentModuleHandle = WinApi.GetModuleHandle((PCWSTR)null);

        fixed (char* pWndClassName = wndClassName)
        {
            var wndClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = &DefaultWndProc,
                lpszClassName = pWndClassName,
                style = WNDCLASS_STYLES.CS_HREDRAW | WNDCLASS_STYLES.CS_VREDRAW,
                hInstance = currentModuleHandle
            };

            return wndClass;
        }
    }

    private static unsafe HWND CreateWindow(string wndClassName, WNDCLASSEXW wndClass, string wndName, bool isMessageOnly)
    {
        fixed (char* lpWndClassName = wndClassName)
        fixed (char* lpWndName = wndName)
        {
            return WinApi.CreateWindowEx(
                0,
                lpWndClassName,
                lpWndName,
                0,
                0, 0, 0, 0,
                isMessageOnly ? HWND_MESSAGE : default,
                default,
                wndClass.hInstance,
                null);
        }
    }

    private static void BuildWindowsName(bool isMessageOnly, out string wndClassName, out string wndName)
    {
        var guid = Guid.NewGuid();
        var msgOnlyStr = isMessageOnly ? "_MsgOnly" : "";
        wndClassName = $"{WndNamePrefix}{msgOnlyStr}Cls";
        wndName = $"{WndNamePrefix}{msgOnlyStr}_{guid}";
    }

    private static int GetMessage(out MSG message)
    {
        return WinApi.GetMessage(out message, default, 0, 0);
    }
    
    #region Dispose

    private bool _disposed;

    ~SimpleWindow()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            //dispose managed state (managed objects)
            if (_captureWndHandle != HWND.Null)
                CloseWindow();
            
            if (!_captureTask.IsCompleted) 
                _captureTask.Wait();

            UnregisterWindowClass(_windowClassName, _windowClass);
        }

        //free unmanaged resources (unmanaged objects) and override finalizer
        //set large fields to null
        _disposed = true;
    }

    private static unsafe void UnregisterWindowClass(string className, WNDCLASSEXW classHandle)
    {
        fixed (char* lpClassName = className)
        {
            WinApi.UnregisterClass(lpClassName, classHandle.hInstance);
        }
    }

    #endregion
}