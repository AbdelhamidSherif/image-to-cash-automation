using System.Diagnostics;
using FlaUI.Core.AutomationElements;

namespace ImageToCash.UiAutomation;

/// <summary>
/// Grounding helpers: poll for a control until it appears, and wait for a list to
/// become stable before trusting its contents (the assessment requires waiting for
/// async search lists to stabilize rather than assuming a fixed layout).
/// </summary>
public static class ControlQuery
{
    public static AutomationElement? WaitFor(Func<AutomationElement?> find, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var sw = Stopwatch.StartNew();
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        while (sw.Elapsed < timeout)
        {
            var el = find();
            if (el is not null) return el;
            Thread.Sleep(interval);
        }
        return null;
    }

    /// <summary>Poll the predicate until it returns true (e.g. a value becomes expected).</summary>
    public static bool WaitUntil(Func<bool> condition, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var sw = Stopwatch.StartNew();
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        while (sw.Elapsed < timeout)
        {
            if (condition()) return true;
            Thread.Sleep(interval);
        }
        return false;
    }

    /// <summary>
    /// Wait until the content of a control stops changing across consecutive reads.
    /// Returns true if it became stable before the timeout.
    /// </summary>
    public static bool WaitStable(Func<string> read, int stableFrames = 3, TimeSpan? timeout = null)
    {
        var sw = Stopwatch.StartNew();
        var limit = timeout ?? TimeSpan.FromSeconds(10);
        string? last = null;
        var frames = 0;
        while (sw.Elapsed < limit)
        {
            var current = read();
            if (current == last)
            {
                frames++;
                if (frames >= stableFrames) return true;
            }
            else
            {
                frames = 0;
                last = current;
            }
            Thread.Sleep(200);
        }
        return false;
    }
}
