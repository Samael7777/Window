using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using PhoenixTools.Window;

namespace Window.Tests;

internal record Message(uint Msg, nuint WParam, nint LParam);

[TestFixture]
public class WindowTests
{
    [Test]
    public void SimpleWindowCreate_Test()
    {
        using var window = new SimpleWindow(false);
        var windows = EnumProcessWindows();

        Assert.That(window.Handle, Is.AnyOf(windows));
    }

    [Test]
    public void InvocationOnWindowThread_Test()
    {
        var currentProcessId = WinApi.GetCurrentThreadId();
        using var window = new SimpleWindow();

        var executionThreadId = window.Invoke(WinApi.GetCurrentThreadId);

        Assert.Multiple(() =>
        {
            Assert.That(executionThreadId, Is.Not.EqualTo(currentProcessId));
            Assert.That(executionThreadId, Is.EqualTo(window.ThreadId));
        });
    }

    [Test]
    public void MessageCapture_Test()
    {
        using var window = new SimpleWindow();
        var msgEvent = new ManualResetEventSlim(false);

        Message? message = null;
        window.MessageReceived += (_, msg) =>
        {
            if (msg.Message != (uint)WindowsMessage.WM_APP) return;

            message = new Message(msg.Message, msg.WParam, msg.LParam);
            msgEvent.Set();

        };

        WinApi.PostMessage((HWND)window.Handle, (uint)WindowsMessage.WM_APP, 10, 20);

        msgEvent.Wait(1000);

        Assert.Multiple(() =>
        {
            Assert.That(message, Is.Not.Null);
            Assert.That(message!.Msg, Is.EqualTo((uint)WindowsMessage.WM_APP));
            Assert.That((int)message.WParam, Is.EqualTo(10));
            Assert.That((int)message.LParam, Is.EqualTo(20));
        });
    }

    private static List<nint> EnumProcessWindows()
    {
        var result = new List<nint>();

        var enumProc = (WNDENUMPROC)((hWnd, _) =>
        {
            result.Add(hWnd);
            return true;
        });

        WinApi.EnumWindows(enumProc, nint.Zero);

        return result;
    }
}