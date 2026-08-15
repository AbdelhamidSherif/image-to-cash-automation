using System.Text.Json;
using ImageToCash.Core;
using ImageToCash.Extraction;

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
    Console.WriteLine("  ImageToCash.Console extract --image <order.png>");
    Console.WriteLine("  ImageToCash.Console help");
}
