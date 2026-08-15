namespace ImageToCash.Extraction;

public sealed record OcrWord(string Text, double X, double Y, double Width, double Height);

public sealed class OcrLine
{
    public required IReadOnlyList<OcrWord> Words { get; init; }
    public string Text => string.Join(" ", Words.Select(w => w.Text));
}

public interface IOcrEngine
{
    Task<IReadOnlyList<OcrLine>> RecognizeAsync(string imagePath, CancellationToken ct = default);
}
