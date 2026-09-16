using System.Drawing.Printing;
using Everest.App.Models;

namespace Everest.App.Printing;

public class PrintJobRunner
{
    private readonly List<Sheet> _sheets;
    private readonly HashSet<string> _hidden;
    private int _index;

    private PaperSize? _paper;
    private bool _landscape;

    /// <summary>Tray for body and blank sheets.</summary>
    public string? TrayName { get; set; }

    /// <summary>Tray for cover sheets. Falls back to TrayName when null.</summary>
    public string? CoverTrayName { get; set; }

    public string? LastWarning { get; private set; }

    private static readonly Dictionary<string, List<PaperSource>> TrayCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static void ClearTrayCache() => TrayCache.Clear();

    public PrintJobRunner(List<Sheet> sheets, HashSet<string>? hidden = null)
    {
        _sheets = sheets;
        _hidden = hidden ?? new HashSet<string>();
    }

    public static List<PaperSource> AllTrays(string printerName)
    {
        if (TrayCache.TryGetValue(printerName, out var cached)) return cached;

        var doc = new PrintDocument();
        try { doc.PrinterSettings.PrinterName = printerName; } catch { }

        var list = doc.PrinterSettings.IsValid
            ? doc.PrinterSettings.PaperSources.Cast<PaperSource>().ToList()
            : new List<PaperSource>();

        TrayCache[printerName] = list;
        return list;
    }

    public static List<PaperSource> Trays(string printerName)
    {
        string[] drop =
        {
            "envelope", "manual", "auto", "automatically", "tractor", "small", "large capacity",
            "مغلف", "يدوي", "تلقائي"
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<PaperSource>();

        foreach (var s in AllTrays(printerName))
        {
            string n = (s.SourceName ?? "").Trim();
            if (n.Length == 0) continue;

            if (s.Kind is PaperSourceKind.Envelope
                       or PaperSourceKind.Manual
                       or PaperSourceKind.ManualFeed
                       or PaperSourceKind.SmallFormat
                       or PaperSourceKind.LargeFormat
                       or PaperSourceKind.TractorFeed)
                continue;

            if (drop.Any(d => n.Contains(d, StringComparison.OrdinalIgnoreCase))) continue;
            if (!seen.Add(n)) continue;

            list.Add(s);
        }

        return list;
    }

    public static List<string> SupportedSizes(string printerName)
    {
        var doc = new PrintDocument();
        try { doc.PrinterSettings.PrinterName = printerName; } catch { }

        return doc.PrinterSettings.PaperSizes.Cast<PaperSize>()
            .Select(p => $"{p.PaperName} — {p.Width / 100.0 * 25.4:0} × {p.Height / 100.0 * 25.4:0} مم")
            .Distinct().OrderBy(s => s).ToList();
    }

    private PrintDocument Build(string printerName, TemplateDef sizeFrom)
    {
        var doc = new PrintDocument { DocumentName = "Everest" };

        if (!string.IsNullOrWhiteSpace(printerName))
            doc.PrinterSettings.PrinterName = printerName;

        if (!doc.PrinterSettings.IsValid)
            throw new InvalidOperationException($"الطابعة \"{printerName}\" غير متاحة.");

        doc.OriginAtMargins = false;
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        int wHund = (int)Math.Round(sizeFrom.PageWidthMm  / 25.4 * 100);
        int hHund = (int)Math.Round(sizeFrom.PageHeightMm / 25.4 * 100);

        var sizes = doc.PrinterSettings.PaperSizes.Cast<PaperSize>().ToList();

        _landscape = false;

        var match = sizes.FirstOrDefault(p =>
            Math.Abs(p.Width - wHund) <= 20 && Math.Abs(p.Height - hHund) <= 20);

        if (match == null)
        {
            var rotated = sizes.FirstOrDefault(p =>
                Math.Abs(p.Width - hHund) <= 20 && Math.Abs(p.Height - wHund) <= 20);
            if (rotated != null) { match = rotated; _landscape = true; }
        }

        if (match == null)
        {
            match = sizes
                .Where(p => p.Width >= wHund - 20 && p.Height >= hHund - 20)
                .OrderBy(p => (long)p.Width * p.Height)
                .FirstOrDefault();

            if (match == null)
            {
                var rot = sizes
                    .Where(p => p.Width >= hHund - 20 && p.Height >= wHund - 20)
                    .OrderBy(p => (long)p.Width * p.Height)
                    .FirstOrDefault();
                if (rot != null) { match = rot; _landscape = true; }
            }

            if (match != null)
                LastWarning =
                    $"مقاس القالب {sizeFrom.PageWidthMm:0}×{sizeFrom.PageHeightMm:0} مم غير معرّف في الطابعة.\n" +
                    $"تمت الطباعة على \"{match.PaperName}\" " +
                    $"({match.Width / 100.0 * 25.4:0}×{match.Height / 100.0 * 25.4:0} مم).";
        }

        if (match == null)
        {
            var names = string.Join("\n", SupportedSizes(printerName).Take(15));
            throw new InvalidOperationException(
                $"لا يوجد مقاس ورق في الطابعة يتسع للقالب " +
                $"({sizeFrom.PageWidthMm:0}×{sizeFrom.PageHeightMm:0} مم).\n\n" +
                $"المقاسات المتاحة:\n{names}");
        }

        _paper = match;
        doc.DefaultPageSettings.PaperSize = _paper;
        doc.DefaultPageSettings.Landscape = _landscape;

        doc.BeginPrint += (_, _) => _index = 0;

        // Per-page tray and orientation — keeps one job, so sheet order is preserved.
        doc.QueryPageSettings += (_, e) =>
        {
            e.PageSettings.PaperSize = _paper!;
            e.PageSettings.Landscape = _landscape;
            e.PageSettings.Margins = new Margins(0, 0, 0, 0);

            if (_index >= _sheets.Count) return;

            string? want = _sheets[_index].Kind == SheetKind.Cover
                ? (CoverTrayName ?? TrayName)
                : TrayName;

            if (string.IsNullOrWhiteSpace(want)) return;

            var tray = doc.PrinterSettings.PaperSources.Cast<PaperSource>()
                .FirstOrDefault(s => s.SourceName == want);

            if (tray != null) e.PageSettings.PaperSource = tray;
            else LastWarning = (LastWarning == null ? "" : LastWarning + "\n") +
                               $"الدرج \"{want}\" غير متاح — تمت الطباعة من الدرج الافتراضي.";
        };

        doc.PrintPage += OnPrintPage;
        return doc;
    }

    private void OnPrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_index >= _sheets.Count) { e.HasMorePages = false; return; }

        try
        {
            if (e.Graphics != null)
                TemplateRenderer.Draw(e.Graphics, _sheets[_index], _hidden);
        }
        catch (Exception ex)
        {
            if (e.Graphics != null)
                e.Graphics.DrawString("خطأ في الورقة: " + ex.Message,
                    new Font("Segoe UI", 10), Brushes.Red, 50, 50);
        }

        _index++;
        e.HasMorePages = _index < _sheets.Count;
    }

    public void Preview(IWin32Window owner, TemplateDef sizeFrom, string printerName)
    {
        using var doc = Build(printerName, sizeFrom);
        using var dlg = new PrintPreviewDialog
        {
            Document = doc,
            Width = 1000,
            Height = 760,
            StartPosition = FormStartPosition.CenterParent,
            UseAntiAlias = true
        };
        dlg.ShowDialog(owner);
    }

    public int Print(TemplateDef sizeFrom, string printerName)
    {
        using var doc = Build(printerName, sizeFrom);
        doc.Print();
        return _index;
    }
}