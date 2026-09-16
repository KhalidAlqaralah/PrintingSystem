using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using Everest.App.Models;

namespace Everest.App.Printing;

public class ReportColumn
{
    public string Header { get; init; } = "";
    public double WidthMm { get; init; } = 20;
    public FieldAlign Align { get; init; } = FieldAlign.Center;
}

public class GridReportRenderer
{
    private const float U = 100f / 25.4f;

    private const double Left = 12, Right = 287;
    private const double HeaderY = 40, HeaderH = 8;
    private const double DataTop = 48, DataBottom = 182;
    private const double RowH = 7, TotalsH = 8;

    private readonly string _title;
    private readonly string _subtitle;
    private readonly List<ReportColumn> _cols;
    private readonly List<string[]> _rows;
    private readonly string _totalsLabel;
    private readonly string _totalsValue;
    private readonly List<List<string[]>> _pages = new();
    private int _page;

    public GridReportRenderer(string title, string subtitle,
                              List<ReportColumn> cols, List<string[]> rows,
                              string totalsLabel, string totalsValue)
    {
        _title = title;
        _subtitle = subtitle;
        _cols = cols;
        _rows = rows;
        _totalsLabel = totalsLabel;
        _totalsValue = totalsValue;

        int perPage = Math.Max(1, (int)((DataBottom - DataTop) / RowH));
        for (int i = 0; i < Math.Max(1, rows.Count); i += perPage)
            _pages.Add(rows.Skip(i).Take(perPage).ToList());
        if (_pages.Count == 0) _pages.Add(new List<string[]>());
    }

    public int PageCount => _pages.Count;
    public string? TrayName { get; set; }

    public PrintDocument BuildDocument(string printerName)
    {
        var doc = new PrintDocument { DocumentName = _title };

        if (!string.IsNullOrWhiteSpace(printerName))
            doc.PrinterSettings.PrinterName = printerName;

        if (!doc.PrinterSettings.IsValid)
            throw new InvalidOperationException($"الطابعة \"{printerName}\" غير متاحة.");

        doc.OriginAtMargins = false;
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        var a4 = doc.PrinterSettings.PaperSizes.Cast<PaperSize>()
            .FirstOrDefault(p => Math.Abs(p.Width - 827) <= 20 && Math.Abs(p.Height - 1169) <= 20);
        if (a4 != null) doc.DefaultPageSettings.PaperSize = a4;
        doc.DefaultPageSettings.Landscape = true;
        ApplyTray(doc, TrayName);

        doc.BeginPrint += (_, _) => _page = 0;
        doc.PrintPage += (_, e) =>
        {
            if (e.Graphics != null) DrawPage(e.Graphics, _page);
            _page++;
            e.HasMorePages = _page < _pages.Count;
        };

        return doc;
    }

    private double[] ColumnEdges()
    {
        // laid out right-to-left starting at Right
        var edges = new double[_cols.Count + 1];
        double x = Right;
        double total = _cols.Sum(c => c.WidthMm);
        double scale = (Right - Left) / Math.Max(1, total);

        edges[0] = x;
        for (int i = 0; i < _cols.Count; i++)
        {
            x -= _cols[i].WidthMm * scale;
            edges[i + 1] = x;
        }
        return edges;
    }

    private void DrawPage(Graphics g, int pageIndex)
    {
        g.PageUnit = GraphicsUnit.Display;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        var rows = _pages[pageIndex];
        bool last = pageIndex == _pages.Count - 1;
        var edges = ColumnEdges();

        using var thick = new Pen(Color.Black, 1.0f);
        using var thin = new Pen(Color.Black, 0.6f);
        using var grey = new Pen(Color.Gray, 0.5f);

        var logo = AppFonts.Asset("logo.pdf") ?? AppFonts.Asset("logo.jpg");
        if (logo != null)
        {
            var img = ArtworkRenderer.Render(logo, 300);
            if (img != null)
                g.DrawImage(img, 239f * U, 10f * U, 47.8f * U, 23.9f * U);
        }

        Text(g, _title, Left, 14, Right - Left, 16, true, FieldAlign.Center);
        Text(g, _subtitle, Left, 25, Right - Left, 10.5, false, FieldAlign.Center);
        g.DrawLine(thin, (float)Left * U, 34 * U, (float)Right * U, 34 * U);

        for (int i = 0; i < _cols.Count; i++)
        {
            double x1 = edges[i], x0 = edges[i + 1];
            Box(g, thick, x0, HeaderY, x1, HeaderY + HeaderH);
            Text(g, _cols[i].Header, x0 + 0.8, HeaderY + HeaderH / 2 - 2.3,
                 x1 - x0 - 1.6, 10.5, true, FieldAlign.Center);
        }

        double y = DataTop;
        foreach (var r in rows)
        {
            for (int i = 0; i < _cols.Count; i++)
            {
                double x1 = edges[i], x0 = edges[i + 1];
                Box(g, thin, x0, y, x1, y + RowH);
                string v = i < r.Length ? r[i] : "";
                Text(g, v, x0 + 0.8, y + RowH / 2 - 1.9, x1 - x0 - 1.6, 9.5, false, _cols[i].Align);
            }
            y += RowH;
        }

        if (last)
        {
            double split = edges[Math.Min(3, edges.Length - 1)];
            Box(g, thick, split, y, Right, y + TotalsH);
            Box(g, thick, Left, y, split, y + TotalsH);

            Text(g, _totalsLabel, split + 2, y + TotalsH / 2 - 2.4, Right - split - 4, 11, true, FieldAlign.Right);
            Text(g, _totalsValue, Left + 1, y + TotalsH / 2 - 2.8, split - Left - 2, 12.5, true, FieldAlign.Center);
        }

        g.DrawLine(thin, (float)Left * U, 194 * U, (float)Right * U, 194 * U);
        Text(g, $"Printed on: {DateTime.Now:dd/MM/yyyy HH:mm}", Left, 195.4, 70, 7.6, false, FieldAlign.Left);
        Text(g, $"Page {pageIndex + 1} of {_pages.Count}", Right - 40, 195.4, 40, 7.6, false, FieldAlign.Right);
    }

    private static void Box(Graphics g, Pen pen, double x0, double y0, double x1, double y1) =>
        g.DrawRectangle(pen, (float)x0 * U, (float)y0 * U,
                             (float)(x1 - x0) * U, (float)(y1 - y0) * U);

    private static void Text(Graphics g, string text, double xMm, double yMm, double wMm,
                             double sizePt, bool bold, FieldAlign align)
    {
        if (string.IsNullOrWhiteSpace(text) || wMm <= 0) return;

        float boxW = (float)wMm * U;
        float size = (float)sizePt;

        var style = bold ? FontStyle.Bold : FontStyle.Regular;
        Font font = AppFonts.Create(AppFonts.PrintFamily, size, style);
        while (size > 5f)
        {
            var m = g.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
            if (m.Width <= boxW) break;
            font.Dispose();
            size -= 0.4f;
            font = AppFonts.Create(AppFonts.PrintFamily, size, style);
        }

        using (font)
        using (var sf = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.DirectionRightToLeft)
        {
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.None,
            Alignment = align switch
            {
                FieldAlign.Center => StringAlignment.Center,
                FieldAlign.Left   => StringAlignment.Far,
                _                 => StringAlignment.Near
            }
        })
        {
            g.DrawString(text, font, Brushes.Black,
                new RectangleF((float)xMm * U, (float)yMm * U, boxW, (float)(sizePt * 0.65) * U), sf);
        }
    }

    internal static void ApplyTray(PrintDocument doc, string? trayName)
    {
        if (string.IsNullOrWhiteSpace(trayName)) return;

        var tray = doc.PrinterSettings.PaperSources.Cast<PaperSource>()
            .FirstOrDefault(s => s.SourceName == trayName);

        if (tray != null) doc.DefaultPageSettings.PaperSource = tray;
    }
}
