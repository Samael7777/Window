# Window 1.0.2
Simple message-only window for receiving windows messages 

Example:
```csharp
using var window = new SimpleWindow();

Debug.WriteLine($"Window name:{window.Name} hWnd:{window.Handle}");

window.MessageReceived += (o, a) =>
{
   a.IsHandled = true;
   Debug.WriteLine($"Message:{a.Message} WParam:{a.WParam} LParam:{a.LParam}");
};

```