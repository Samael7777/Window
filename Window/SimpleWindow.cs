using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using PhoenixTools.Window.Internal;

namespace PhoenixTools.Window;

public sealed partial class SimpleWindow : IDisposable
{
    private const string InvokeActionMessageName = "InvokeActionMessage{8FD8734C-9B9D-4866-944B-54B81B9E3D7C}";
    private const string WndNamePrefix = "PhoenixToolsWindow";
    private const string WindowClassName = "PhoenixToolsWindow_Class";
    private const string MessageWindowClassName = "PhoenixToolsWindow_MessageOnly_Class";
    private const int MessageWindow = -3;

    private delegate int WndProcDelegate(HWND hWnd, uint message, WPARAM wParam, LPARAM lParam);

    private static readonly object s_lock = new();
    private static readonly ConcurrentDictionary<HWND, WndProcDelegate> s_wndProcDelegates = new();
    private static readonly uint s_invokeActionMessage = WinApi.RegisterWindowMessage(InvokeActionMessageName);
    private static readonly Lazy<WNDCLASSEXW> s_wndClass;
    private static readonly Lazy<WNDCLASSEXW> s_msgOnlyWndClass;

    private static int s_windowsInstancesCount;
    private static int s_msgOnlyInstancesCount;

    private readonly ConcurrentQueue<InvocationResult> _invocationQueue = new();
    private readonly Task _captureTask;

    private HWND _captureWndHandle;

    public event EventHandler<WindowDestroyedEventArgs>? WindowDestroyed;
    public event EventHandler<WindowsMessageEventArgs>? MessageReceived;

    public nint Handle => _captureWndHandle;
    public int ThreadId { get; private set; }
    public string Name { get; }
    public bool IsMessageOnly { get; }

    static SimpleWindow()
    {
        s_wndClass = new Lazy<WNDCLASSEXW>(() => BuildWindowClass(WindowClassName));
        s_msgOnlyWndClass = new Lazy<WNDCLASSEXW>(() => BuildWindowClass(MessageWindowClassName));
    }

    public SimpleWindow(bool isMessageOnly = true)
    {
        IsMessageOnly = isMessageOnly;

        Name = BuildWindowName();
        RegisterWindowClass();
        _captureTask = RunCaptureTask(Name);
        IncrementWindowInstancesCount();
    }

    private Task RunCaptureTask(string windowName)
    {
        using var waitInitEvent = new ManualResetEventSlim(false);

        // ReSharper disable once AccessToDisposedClosure
        var captureTask = new Task(() => CaptureTask(windowName, waitInitEvent),
            TaskCreationOptions.LongRunning
            | TaskCreationOptions.DenyChildAttach
            | TaskCreationOptions.HideScheduler);

        captureTask.Start();
        waitInitEvent.Wait();
        
        return captureTask;
    }

    private void CaptureTask(string windowName, ManualResetEventSlim initEvent)
    {
        ThreadId = (int)WinApi.GetCurrentThreadId();
        var wndClassName = GetWindowClassName();
        var wndClass = GetWindowClass();
        _captureWndHandle = CreateWindow(wndClassName, wndClass, windowName, IsMessageOnly);
        
        if (_captureWndHandle.IsNull)
            throw new Win32Exception();

        s_wndProcDelegates.TryAdd(_captureWndHandle, WndProc);

        initEvent.Set();

        var isRunning = true;
        while (isRunning)
        {
            var error = GetMessage(out var message);

            switch (error)
            {
                case -1:
                    //Handle the error
                    isRunning = false;
                    break;
                case 0:
                    //Close window
                    isRunning = false;
                    break;
                default:
                    WinApi.TranslateMessage(in message);
                    _ = WinApi.DispatchMessage(in message);
                    break;
            }
        }

        s_wndProcDelegates.TryRemove(_captureWndHandle, out _);

        DecrementWindowInstancesCount();

        var instanceCount = IsMessageOnly ? s_msgOnlyInstancesCount : s_windowsInstancesCount;
        if (instanceCount == 0) UnregisterWindowClass();

        var args = new WindowDestroyedEventArgs(Handle, ThreadId);
        _captureWndHandle = HWND.Null;
        ThreadId = -1;

        WindowDestroyed?.Invoke(this, args);
    }

    private void CloseWindow()
    {
        Invoke(() => WinApi.DestroyWindow(_captureWndHandle));
    }

    private string BuildWindowName()
    {
        var guid = Guid.NewGuid();
        var msgOnlyStr = IsMessageOnly ? "_MsgOnly" : "";

        return $"{WndNamePrefix}{msgOnlyStr}_{guid}";
    }

    private void RegisterWindowClass()
    {
        lock (s_lock)
        {
            var count = IsMessageOnly ? s_msgOnlyInstancesCount : s_windowsInstancesCount;

            if (count > 0) return; //Already registered

            var wndClass = GetWindowClass();
            var wndClassHandle = WinApi.RegisterClassEx(wndClass);
            if (wndClassHandle == 0)
                throw new Win32Exception();
        }
    }

    private string GetWindowClassName()
    {
        return IsMessageOnly ? MessageWindowClassName : WindowClassName;
    }

    private WNDCLASSEXW GetWindowClass()
    {
        return IsMessageOnly ? s_msgOnlyWndClass.Value : s_wndClass.Value;
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

    private static unsafe HWND CreateWindow(string wndClassName, WNDCLASSEXW wndClass, string wndName,
        bool isMessageOnly)
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
                isMessageOnly ? (HWND)(nint)MessageWindow : default,
                default,
                wndClass.hInstance,
                null);
        }
    }

    private static int GetMessage(out MSG message)
    {
        return WinApi.GetMessage(out message, default, 0, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void IncrementWindowInstancesCount()
    {
        if (IsMessageOnly)
            Interlocked.Increment(ref s_msgOnlyInstancesCount);
        else
            Interlocked.Increment(ref s_windowsInstancesCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecrementWindowInstancesCount()
    {
        if (IsMessageOnly)
            Interlocked.Decrement(ref s_msgOnlyInstancesCount);
        else
            Interlocked.Decrement(ref s_windowsInstancesCount);
    }

    private unsafe void UnregisterWindowClass()
    {
        lock (s_lock)
        {
            var wndClassName = GetWindowClassName();
            var wndClass = GetWindowClass();

            fixed (char* lpClassName = wndClassName)
            {
                WinApi.UnregisterClass(lpClassName, wndClass.hInstance);
            }
        }
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
            //dispose managed state (managed objects)
            if (_captureWndHandle != HWND.Null)
            {
                CloseWindow();
                _captureTask.GetAwaiter().GetResult();
            }

        //free unmanaged resources (unmanaged objects) and override finalizer
        //set large fields to null
        _disposed = true;
    }

    #endregion
}