# Window v.1.0.0
Simple message-only window for receiving windows messages 

Example:
```csharp
using var window = new SimpleWindow();

Debug.WriteLine($"Window name:{window.Name} hWnd:{window.Handle}");

window.MessageReceived += (o, a) =>
{
   Debug.WriteLine($"Message:{a.Message} WParam:{a.WParam} LParam:{a.LParam}");
};

```