using System.Collections.Concurrent;
using SkiaSharp;

namespace Everest.App.Printing;

public static class ArtworkRenderer
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

    private static string Key(string path, int dpi) =>
        $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}|{dpi}";

    /// <summary>Renders page 1 of a PDF, or loads a PNG/JPG. Returns null on failure.</summary>
    public static Bitmap? Render(string path, int dpi = 150)
    {
        if (!File.Exists(path)) return null;

        string key = Key(path, dpi);
        if (Cache.TryGetValue(key, out var hit)) return hit;

        try
        {
            Bitmap bmp;
            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".pdf")
            {
                byte[] bytes = File.ReadAllBytes(path);
                using SKBitmap sk = PDFtoImage.Conversion.ToImage(
                    bytes,
                    password: null,
                    page: 0,
                    options: new PDFtoImage.RenderOptions(Dpi: dpi));

                using var data = sk.Encode(SKEncodedImageFormat.Png, 100);
                using var ms = new MemoryStream(data.ToArray());
                bmp = new Bitmap(ms);
            }
            else
            {
                using var tmp = new Bitmap(path);
                bmp = new Bitmap(tmp);
            }

            Cache[key] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static void Clear()
    {
        foreach (var b in Cache.Values) b.Dispose();
        Cache.Clear();
    }
}