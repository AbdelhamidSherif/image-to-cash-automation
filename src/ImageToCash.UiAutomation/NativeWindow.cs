using System.Runtime.InteropServices;

namespace ImageToCash.UiAutomation;

internal static class NativeWindow
{
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void BringOnScreenAndActivate(IntPtr hwnd, int x = 40, int y = 40)
    {
        if (hwnd == IntPtr.Zero) return;
        ShowWindow(hwnd, 9); // SW_RESTORE
        SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);
    }
}
