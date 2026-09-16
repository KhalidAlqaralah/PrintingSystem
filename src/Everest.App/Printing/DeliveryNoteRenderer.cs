using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using Everest.App.Data;
using Everest.App.Models;

namespace Everest.App.Printing;

public class DeliveryNoteRenderer
{
    private const float U = 100f / 25.4f;          // mm → 1/100 inch

    private readonly TemplateDef _t;
    private readonly DeliveryNoteData _d;
    private readonly List<List<NoteRow>> _pages = new();
    private int _page;

    public DeliveryNoteRenderer(TemplateDef t, DeliveryNoteData d)
    {
        _t = t;
        _d = d;

        var spec = t.Table ?? new TableSpec();

        // Stale or degenerate spec (e.g. JSON written by an older column shape) — rebuild it.
        bool broken = spec.Columns.Count == 0
                   || spec.Columns.Any(c => c.X1Mm - c.X0Mm < 1)
                   || spec.DataBottomMm - spec.DataTopMm < 10;

        if (broken)
        {
            spec = DefaultTemplates.DeliveryNote().Table!;
            _t.Table = spec;
            TemplateStore.Save(_t);          // heal the file on disk
        }

        int perPage = Math.Max(1, spec.AbsoluteMaxRows);

        for (int i = 0; i < Math.Max(1, d.Rows.Count); i += perPage)
            _pages.Add(d.Rows.Skip(i).Take(perPage).ToList());

        if (_pages.Count == 0) _pages.Add(new List<NoteRow>());
    }

    public int PageCount => _pages.Count;
    public string? TrayName { get; set; }

    public PrintDocument BuildDocument(string printerName)
    {
        var doc = new PrintDocument { DocumentName = "Everest — سند التسليم" };

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
        GridReportRenderer.ApplyTray(doc, TrayName);

        doc.BeginPrint += (_, _) => _page = 0;
        doc.PrintPage += (_, e) =>
        {
            if (e.Graphics != null) DrawPage(e.Graphics, _page);
            _page++;
            e.HasMorePages = _page < _pages.Count;
        };

        return doc;
    }

    private void DrawPage(Graphics g, int pageIndex)
    {
        g.PageUnit = GraphicsUnit.Display;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        float ox = (float)_t.OffsetXMm * U;
        float oy = (float)_t.OffsetYMm * U;
        g.TranslateTransform(ox, oy);

        var spec = _t.Table ?? new TableSpec();
        var rows = _pages[pageIndex];
        bool lastPage = pageIndex == _pages.Count - 1;

        if (_d.Rows.Count == 0)
            Text(g, "لا توجد بيانات — تحقق من التاريخ وفلتر \"المطبوعة فقط\"",
                 60, 100, 180, 12, true, FieldAlign.Center);

        DrawLogo(g);
        DrawHeader(g, spec, pageIndex);

        double rowH = spec.RowHeightFor(_d.Rows.Count);
        double fs   = spec.FontSizeFor(rowH);

        DrawTableHeader(g, spec);
        double y = DrawRows(g, spec, rows, rowH, fs);

        if (lastPage) DrawTotals(g, spec, y);

        DrawFooter(g, spec, lastPage ? y + spec.TotalsHeightMm : y, pageIndex);
    }

    // ---------- pieces ----------

    private void DrawLogo(Graphics g)
    {
        string? path = null;

        if (!string.IsNullOrWhiteSpace(_t.LogoFile))
        {
            var inFolder = Path.Combine(AppPaths.FolderFor(_t.Category), _t.LogoFile);
            if (File.Exists(inFolder)) path = inFolder;
            else path = AppFonts.Asset(_t.LogoFile);
        }

        path ??= AppFonts.Asset("logo.pdf")
              ?? AppFonts.Asset("logo.jpg")
              ?? AppFonts.Asset("logo.png");

        if (path == null) return;

        var img = ArtworkRenderer.Render(path, 300);
        if (img == null) return;

        double w = _t.LogoWidthMm  > 1 ? _t.LogoWidthMm  : 47.8;
        double h = _t.LogoHeightMm > 1 ? _t.LogoHeightMm : 23.9;

        g.DrawImage(img,
            (float)_t.LogoXMm * U, (float)_t.LogoYMm * U,
            (float)w * U, (float)h * U);
    }

    private void DrawHeader(Graphics g, TableSpec spec, int pageIndex)
    {
        using var pen = new Pen(Color.Black, 0.8f);

        Box(g, pen, 10, 18, 50, 26);
        Box(g, pen, 10, 26, 50, 42);
        Text(g, "نقابة الأطباء الأردنية", 11, 19.5, 38, 10, false, FieldAlign.Center);
        Text(g, "سند تسليم دفاتر الوصفات", 11, 28.5, 38, 10, false, FieldAlign.Center);
        Text(g, "الطبية لمستحضرات المخدرات", 11, 34.5, 38, 10, false, FieldAlign.Center);

        Box(g, pen, 258, 32, 286, 38);
        Box(g, pen, 222, 32, 258, 38);
        Box(g, pen, 258, 38, 286, 44);
        Box(g, pen, 222, 38, 258, 44);

        Text(g, "التاريخ :",          259, 33.2, 26, 10.5, true,  FieldAlign.Right);
        Text(g, _d.NoteDate.ToString(AppTheme.DateFormat), 223, 33.2, 34, 10, false, FieldAlign.Center);
        Text(g, "رقـــم سند التسليم :", 259, 39.2, 26, 10.5, true,  FieldAlign.Right);
        Text(g, _d.NoteNumber,        223, 39.2, 34, 10, false, FieldAlign.Center);

        g.DrawLine(pen, 10 * U, 46 * U, 286.7f * U, 46 * U);
    }

    private void DrawTableHeader(Graphics g, TableSpec spec)
    {
        using var pen = new Pen(Color.Black, 0.8f);
        double y0 = spec.HeaderYMm, y1 = y0 + spec.HeaderHeightMm;

        foreach (var c in spec.Columns)
        {
            Box(g, pen, c.X0Mm, y0, c.X1Mm, y1);
            Text(g, c.Header, c.X0Mm + 0.8, y0 + spec.HeaderHeightMm / 2 - 2.2,
                 c.X1Mm - c.X0Mm - 1.6, spec.FontSize, true, FieldAlign.Center);
        }
    }

    private double DrawRows(Graphics g, TableSpec spec, List<NoteRow> rows,
                            double rowH, double fs)
    {
        using var pen = new Pen(Color.Black, 0.6f);
        double y = spec.DataTopMm;

        foreach (var r in rows)
        {
            foreach (var c in spec.Columns)
            {
                Box(g, pen, c.X0Mm, y, c.X1Mm, y + rowH);
                Text(g, r.Value(c.Token), c.X0Mm + 0.8, y + rowH / 2 - fs * 0.19,
                     c.X1Mm - c.X0Mm - 1.6, fs, false, c.Align);
            }
            y += rowH;
        }

        return y;
    }

    private void DrawTotals(Graphics g, TableSpec spec, double y)
    {
        using var pen = new Pen(Color.Black, 1.1f);
        double h = spec.TotalsHeightMm;

        Box(g, pen, spec.TotalsSplitXMm, y, spec.RightEdgeMm, y + h);
        Box(g, pen, spec.LeftEdgeMm, y, spec.TotalsSplitXMm, y + h);

        Text(g, spec.TotalsLabel, spec.TotalsSplitXMm + 2, y + h / 2 - 2.2,
             60, spec.FontSize * 0.95, true, FieldAlign.Left);

        Text(g, _d.TotalNotebooks.ToString(),
             spec.LeftEdgeMm + 1, y + h / 2 - 2.6,
             spec.TotalsSplitXMm - spec.LeftEdgeMm - 2,
             spec.FontSize * 1.15, true, FieldAlign.Center);
    }

    private void DrawFooter(Graphics g, TableSpec spec, double tableBottom, int pageIndex)
    {
        using var pen = new Pen(Color.Black, 0.6f);
        using var grey = new Pen(Color.Gray, 0.5f);

        double sig1 = Math.Min(178, Math.Max(tableBottom + 13, 86));
        double sig2 = Math.Min(190, sig1 + 16);

        Text(g, "اسم المستلم:", 251.7, sig1 - 4.5, 30, 10.5, false, FieldAlign.Right);
        g.DrawLine(grey, 148 * U, (float)sig1 * U, 282 * U, (float)sig1 * U);
        g.DrawLine(grey, 11 * U, (float)sig1 * U, 146.4f * U, (float)sig1 * U);

        Text(g, "التوقيع والختم:", 251.7, sig2 - 4.5, 30, 10.5, false, FieldAlign.Right);
        g.DrawLine(grey, 148 * U, (float)sig2 * U, 282 * U, (float)sig2 * U);

        Text(g,
            "بالتوقيع على سند التسليم هذا يقر المستلم ويؤكد بأنه تم التأكد من عدد الدفاتر المرفقة لكل طبيب وعليه تم التوقيع",
            40, sig1 - 4.5, 190, 8.5, false, FieldAlign.Center);

        g.DrawLine(pen, 10 * U, 194 * U, 287 * U, 194 * U);

        Text(g, $"Printed on: {DateTime.Now:dd/MM/yyyy HH:mm}", 10, 195.4, 60, 7.6, false, FieldAlign.Left);
        Text(g, $"Page {pageIndex + 1} of {_pages.Count}", 250, 195.4, 36, 7.6, false, FieldAlign.Left);
    }

    // ---------- primitives ----------

    private static void Box(Graphics g, Pen pen, double x0, double y0, double x1, double y1) =>
        g.DrawRectangle(pen, (float)x0 * U, (float)y0 * U,
                             (float)(x1 - x0) * U, (float)(y1 - y0) * U);

    private static void Text(Graphics g, string text, double xMm, double yMm, double wMm,
                             double sizePt, bool bold, FieldAlign align)
    {
        if (string.IsNullOrWhiteSpace(text) || wMm <= 0) return;

        float boxW = (float)wMm * U;
        float size = (float)sizePt;

        // shrink until it fits, down to 5pt
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
                new RectangleF((float)xMm * U, (float)yMm * U, boxW, (float)(sizePt * 0.6) * U), sf);
        }
    }
}