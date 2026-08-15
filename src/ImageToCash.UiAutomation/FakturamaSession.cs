using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ImageToCash.UiAutomation;

public sealed class FakturamaSession : IDisposable
{
    private readonly UIA3Automation _automation = new();
    private Application? _app;

    public UIA3Automation Automation => _automation;
    public Window? MainWindow { get; private set; }
    public int ProcessId => _app?.ProcessId ?? 0;

    public void Launch(string exePath)
    {
        _app = Application.Launch(exePath);
        if (!WaitForMainWindow())
            throw new InvalidOperationException("Fakturama main window did not appear in time.");
    }

    public bool Attach(int processId)
    {
        _app = Application.Attach(processId);
        return WaitForMainWindow();
    }

    public void EnsureVisible(int x = 40, int y = 40)
    {
        if (MainWindow is null) return;
        var hwnd = MainWindow.Properties.NativeWindowHandle.IsSupported
            ? new IntPtr(MainWindow.Properties.NativeWindowHandle.Value)
            : IntPtr.Zero;
        if (hwnd == IntPtr.Zero) return;
        NativeWindow.BringOnScreenAndActivate(hwnd, x, y);
    }

    public bool WaitForMainWindow(TimeSpan? timeout = null)
    {
        if (_app is null) return false;
        var limit = timeout ?? TimeSpan.FromSeconds(60);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < limit)
        {
            var window = _app.GetAllTopLevelWindows(_automation)
                .FirstOrDefault(w => !string.IsNullOrEmpty(w.Title));
            if (window is not null)
            {
                MainWindow = window;
                return true;
            }
            Thread.Sleep(250);
        }
        return false;
    }

    public void Dispose()
    {
        _app?.Dispose();
        _automation.Dispose();
    }
}
