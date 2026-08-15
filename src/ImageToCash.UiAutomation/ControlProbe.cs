using System.Text;
using FlaUI.Core.AutomationElements;

namespace ImageToCash.UiAutomation;

public static class ControlProbe
{
    /// <summary>Recursively dump the UIA control tree for inspection / grounding.</summary>
    public static string DumpTree(AutomationElement root, int maxDepth = 12)
    {
        var sb = new StringBuilder();
        Dump(root, sb, 0, maxDepth);
        return sb.ToString();
    }

    private static void Dump(AutomationElement el, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        var name = Safe(el.Properties.Name.ValueOrDefault);
        var aid = Safe(el.Properties.AutomationId.ValueOrDefault);
        var ct = el.Properties.ControlType.IsSupported ? el.Properties.ControlType.Value.ToString() : string.Empty;
        var cls = Safe(el.Properties.ClassName.ValueOrDefault);
        var rect = el.Properties.BoundingRectangle.IsSupported ? el.Properties.BoundingRectangle.Value : default;

        sb.Append(indent)
          .Append(ct).Append(" name='").Append(name).Append('\'')
          .Append(" aid='").Append(aid).Append('\'')
          .Append(" class='").Append(cls).Append('\'');
        if (rect.IsEmpty == false)
            sb.Append(" rect=").Append(rect.X).Append(',').Append(rect.Y).Append(' ').Append(rect.Width).Append('x').Append(rect.Height);
        sb.AppendLine();

        foreach (var child in el.FindAllChildren())
            Dump(child, sb, depth + 1, maxDepth);
    }

    private static string Safe(string? s) => s ?? string.Empty;
}
