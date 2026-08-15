using System.Text.Json;
using ImageToCash.Core;
using ImageToCash.Extraction;
using ImageToCash.Flow;
using ImageToCash.UiAutomation;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    var mode = args.Length > 0 ? args[0] : "help";
    try
    {
        switch (mode)
        {
            case "extract":
                return await ExtractAsync(args);
            case "probe":
                return Probe(args);
            case "openorder":
                return await OpenOrder(args);
            case "run":
                return await RunFlow(args);
            default:
                PrintHelp();
                return 0;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR: {ex.Message}");
        return 1;
    }
}

static async Task<int> ExtractAsync(string[] args)
{
    var image = Arg(args, "--image") ?? Arg(args, "-i");
    if (image is null) throw new ArgumentException("--image is required.");
    if (!File.Exists(image)) throw new FileNotFoundException($"Image not found: {image}");

    IOrderExtractor extractor = new HeuristicOrderExtractor(new WindowsOcrEngine());
    var result = await extractor.ExtractAsync(image);

    if (args.Contains("--raw"))
        Console.WriteLine("===== RAW OCR =====\n" + result.RawText + "\n===== END RAW =====");

    if (args.Contains("--debug"))
    {
        var ocr = new WindowsOcrEngine();
        foreach (var line in await ocr.RecognizeAsync(image))
            Console.WriteLine($"Y={line.Words[0].Y:N0}  " + string.Join(" | ", line.Words.Select(w => $"{w.Text}@{w.X:N0}")));
    }

    Console.WriteLine(JsonSerializer.Serialize(result.Order, new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    }));
    if (result.Warnings.Count > 0)
    {
        Console.WriteLine("\nWARNINGS:");
        foreach (var w in result.Warnings) Console.WriteLine("  - " + w);
    }
    return 0;
}

static async Task<int> OpenOrder(string[] args)
{
    var attach = Arg(args, "--attach");
    var pid = int.Parse(attach!);
    using var session = new FakturamaSession();
    if (!session.Attach(pid)) { Console.Error.WriteLine("attach failed"); return 1; }
    session.EnsureVisible();
    Thread.Sleep(1000);

    var button = ControlQuery.WaitFor(
        () => session.MainWindow!.FindAllDescendants()
            .FirstOrDefault(e => e.Properties.Name.ValueOrDefault == "Create: New Order"),
        TimeSpan.FromSeconds(15));
    if (button is null) { Console.Error.WriteLine("Order toolbar button not found."); return 1; }
    Console.WriteLine($"Clicking {button.Properties.Name.Value}");
    button.Click();
    Thread.Sleep(2500);

    var editor = ControlQuery.WaitFor(
        () => session.MainWindow!.FindAllDescendants()
            .FirstOrDefault(e => e.Properties.ClassName.ValueOrDefault == "SWT_Window0"
                                 && e.Properties.Name.ValueOrDefault.Contains("Order", StringComparison.OrdinalIgnoreCase)),
        TimeSpan.FromSeconds(15));
    if (editor is null) { Console.Error.WriteLine("Order editor not found."); return 1; }

    Console.WriteLine("===== NEW ORDER EDITOR TREE =====");
    Console.WriteLine(ControlProbe.DumpTree(editor, 12));
    return 0;
}

static async Task<int> RunFlow(string[] args)
{
    var image = Arg(args, "--image") ?? Arg(args, "-i")
        ?? throw new ArgumentException("--image is required.");
    var attach = Arg(args, "--attach");
    var launch = Arg(args, "--launch");
    var dryRun = !args.Contains("--live");
    var shotDir = Arg(args, "--shot-dir");

    IOrderExtractor extractor = new HeuristicOrderExtractor(new WindowsOcrEngine());
    FlowOptions options = new() { DryRun = dryRun, ScreenshotDir = shotDir };

    if (launch is null && attach is null)
    {
        Console.WriteLine("Probing for a running Fakturama instance...");
        var proc = System.Diagnostics.Process.GetProcessesByName("Fakturama").FirstOrDefault();
        if (proc is null) throw new InvalidOperationException("No running Fakturama; pass --launch <exe> or --attach <pid>.");
        attach = proc.Id.ToString();
    }

    using var session = new FakturamaSession();
    if (launch is not null) session.Launch(launch);
    else if (!session.Attach(int.Parse(attach!))) throw new InvalidOperationException("Could not attach.");
    session.EnsureVisible();
    Thread.Sleep(800);

    IFakturamaAutomation ui = new FakturamaDriver(session, m => Console.WriteLine("  [ui] " + m));
    var flow = new ImageToCashFlow(extractor, ui, options);
    var result = await flow.RunAsync(image);

    Console.WriteLine("\n=== FLOW RESULT ===");
    foreach (var s in result.Steps)
        Console.WriteLine($"[{s.Index:D2}] [{s.Status,-12}] {s.Stage} | {s.Action}" + (s.Detail is null ? "" : $" | {s.Detail}"));
    Console.WriteLine($"Completed: {result.Completed}");
    if (result.Error is not null) Console.WriteLine($"Flow error: {result.Error}");
    return result.Completed ? 0 : 2;
}

static int Probe(string[] args)
{
    var launch = Arg(args, "--launch");
    var attach = Arg(args, "--attach");
    var maxDepth = int.TryParse(Arg(args, "--depth"), out var d) ? d : 12;

    using var session = new FakturamaSession();
    if (launch is not null)
    {
        Console.WriteLine($"Launching {launch} ...");
        session.Launch(launch);
    }
    else if (attach is not null && int.TryParse(attach, out var pid))
    {
        Console.WriteLine($"Attaching to pid {pid} ...");
        if (!session.Attach(pid)) { Console.Error.WriteLine("Could not attach/find main window."); return 1; }
    }
    else
    {
        Console.Error.WriteLine("Specify --launch <exe> or --attach <pid>.");
        return 1;
    }

    Console.WriteLine($"Main window: '{session.MainWindow!.Title}' pid={session.ProcessId}");
    var tree = ControlProbe.DumpTree(session.MainWindow, maxDepth);
    Console.WriteLine(tree);
    return 0;
}

static string? Arg(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}

static void PrintHelp()
{
    Console.WriteLine("Fakturama Image-to-Cash");
    Console.WriteLine("Usage:");
    Console.WriteLine("  ImageToCash.Console extract --image <order.png> [--raw] [--debug]");
    Console.WriteLine("  ImageToCash.Console probe --launch <Fakturama.exe> [--depth N]");
    Console.WriteLine("  ImageToCash.Console probe --attach <pid> [--depth N]");
    Console.WriteLine("  ImageToCash.Console openorder --attach <pid>");
    Console.WriteLine("  ImageToCash.Console run --image <order.png> [--attach <pid>|--launch <exe>] [--live] [--shot-dir <dir>]");
    Console.WriteLine("  ImageToCash.Console help");
}
