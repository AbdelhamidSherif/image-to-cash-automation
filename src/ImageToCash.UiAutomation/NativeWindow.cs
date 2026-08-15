using System.Runtime.InteropServices;

namespace ImageToCash.UiAutomation;

internal static class NativeWindow
{
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>
    /// Restore, move on-screen and force to foreground. Uses the AttachThreadInput trick to
    /// bypass the Windows foreground-lock that normally blocks SetForegroundWindow from a
    /// background console process.
    /// </summary>
    public static void BringOnScreenAndActivate(IntPtr hwnd, int x = 40, int y = 40)
    {
        if (hwnd == IntPtr.Zero) return;
        ShowWindow(hwnd, SW_RESTORE);
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);

        var foreground = GetForegroundWindow();
        GetWindowThreadProcessId(foreground, out _);
        GetWindowThreadProcessId(hwnd, out var targetThread);
        var fgThread = GetWindowThreadProcessId(foreground, out _);

        if (fgThread != targetThread)
        {
            AttachThreadInput(fgThread, targetThread, true);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            AttachThreadInput(fgThread, targetThread, false);
        }
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
    }
}
