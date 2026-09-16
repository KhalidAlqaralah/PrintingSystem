using System.Drawing.Printing;
using Everest.App.Data;
using Everest.App.Models;

namespace Everest.App.Printing;

/// <summary>Renders the audit sample (العينة) through GDI+ to a PDF file — export only, no printer/tray.</summary>
public static class SampleExporter
{
    private const string PdfPrinter = "Microsoft Print to PDF";

    /// <summary>Exports the sample for the entity to outputPath. Returns a warning to show, or null.</summary>
    public static string? Export(Entity entity, string outputPath)
    {
        bool available = PrinterSettings.InstalledPrinters.Cast<string>()
            .Any(p => p.Equals(PdfPrinter, StringComparison.OrdinalIgnoreCase));

        if (!available)
            throw new InvalidOperationException(
                $"طابعة \"{PdfPrinter}\" غير مثبّتة على هذا الجهاز.\n\n" +
                "فعّلها من: إعدادات ويندوز ← الأجهزة ← الطابعات.");

        bool hospital = entity.Type == EntityType.Hospital;
        string wantedFile = hospital ? "عينة_المستشفى.pdf" : "عينة_الدكتور.pdf";

        var t = TemplateStore.LoadCategory(TemplateCategory.Sample)
            .FirstOrDefault(x => string.Equals(x.PdfFile, wantedFile, StringComparison.OrdinalIgnoreCase));

        if (t == null)
            throw new InvalidOperationException($"لا يوجد قالب عينة لهذا النوع ({entity.TypeLabel}).");

        var sheet = new Sheet
        {
            Kind = SheetKind.Body,
            Template = t,
            Right = new PrintData { Entity = entity },
            Label = "عينة"
        };

        using var doc = new PrintDocument { DocumentName = "Everest — عينة" };
        doc.PrinterSettings.PrinterName = PdfPrinter;

        if (!doc.PrinterSettings.IsValid)
            throw new InvalidOperationException($"الطابعة \"{PdfPrinter}\" غير متاحة.");

        doc.PrinterSettings.PrintToFile = true;
        doc.PrinterSettings.PrintFileName = outputPath;

        doc.OriginAtMargins = false;
        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        int wHund = (int)Math.Round(t.PageWidthMm  / 25.4 * 100);
        int hHund = (int)Math.Round(t.PageHeightMm / 25.4 * 100);
        doc.DefaultPageSettings.PaperSize = new PaperSize("عينة", wHund, hHund);

        doc.PrintPage += (_, e) =>
        {
            if (e.Graphics != null) TemplateRenderer.Draw(e.Graphics, sheet);
            e.HasMorePages = false;
        };

        string? warning = null;

        try
        {
            doc.Print();
        }
        catch
        {
            var fallback = doc.PrinterSettings.PaperSizes.Cast<PaperSize>()
                .Where(p => p.Width >= wHund && p.Height >= hHund)
                .OrderBy(p => (long)p.Width * p.Height)
                .FirstOrDefault();

            if (fallback == null)
                throw new InvalidOperationException(
                    $"لا يوجد مقاس ورق في طابعة PDF يتسع لصفحة العينة ({t.PageWidthMm:0}×{t.PageHeightMm:0} مم).");

            doc.DefaultPageSettings.PaperSize = fallback;
            warning = $"مقاس العينة ({t.PageWidthMm:0}×{t.PageHeightMm:0} مم) غير مدعوم مباشرة على طابعة PDF.\n" +
                      $"تم التصدير على مقاس \"{fallback.PaperName}\" " +
                      $"({fallback.Width / 100.0 * 25.4:0}×{fallback.Height / 100.0 * 25.4:0} مم).";

            doc.Print();
        }

        return warning;
    }
}
