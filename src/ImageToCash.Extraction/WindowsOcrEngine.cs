using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace ImageToCash.Extraction;

public sealed class WindowsOcrEngine : IOcrEngine
{
    private readonly OcrEngine _engine;

    public WindowsOcrEngine()
    {
        _engine = OcrEngine.TryCreateFromUserProfileLanguages()
                  ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"))
                  ?? throw new InvalidOperationException("No OCR engine available on this system.");
    }

    public async Task<IReadOnlyList<OcrLine>> RecognizeAsync(string imagePath, CancellationToken ct = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(ct);
        using var stream = await file.OpenAsync(FileAccessMode.Read).AsTask(ct);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct);
        var bitmap = await decoder.GetSoftwareBitmapAsync().AsTask(ct);
        var result = await _engine.RecognizeAsync(bitmap).AsTask(ct);

        var lines = new List<OcrLine>();
        foreach (var line in result.Lines)
        {
            var words = new List<OcrWord>();
            foreach (var w in line.Words)
            {
                var r = w.BoundingRect;
                words.Add(new OcrWord(w.Text, r.X, r.Y, r.Width, r.Height));
            }
            lines.Add(new OcrLine { Words = words });
        }
        return lines;
    }
}
