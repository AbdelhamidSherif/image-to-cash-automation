using ImageToCash.Extraction;

namespace ImageToCash.Tests;

public sealed class FakeOcrEngine : IOcrEngine
{
    private readonly List<OcrLine> _lines;
    public FakeOcrEngine(params (double Y, (string Text, double X)[] Words)[] rows)
    {
        _lines = rows
            .Select(r => new OcrLine
            {
                Words = r.Words.Select(w => new OcrWord(w.Text, w.X, r.Y, 0, 0)).ToList()
            })
            .ToList();
    }

    public Task<IReadOnlyList<OcrLine>> RecognizeAsync(string imagePath, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OcrLine>>(_lines);
}
