namespace ImageToCash.Core;

public interface IOrderExtractor
{
    Task<ExtractionResult> ExtractAsync(string imagePath, CancellationToken ct = default);
}

public sealed class ExtractionResult
{
    public OrderInfo Order { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
}
