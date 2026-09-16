using System.Drawing.Drawing2D;
using Everest.App.Data;
using Everest.App.Models;

namespace Everest.App.Printing;

public static class TemplateRenderer
{
    /// <summary>Page units are 1/100 inch (GraphicsUnit.Display).</summary>
    private const float MmToUnits = 100f / 25.4f;

    public static void Draw(Graphics g, Sheet sheet, HashSet<string>? hidden = null)
    {
        if (sheet.Kind == SheetKind.Blank || sheet.Template == null) return;

        var t = sheet.Template;
        g.PageUnit = GraphicsUnit.Display;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        float ox = (float)t.OffsetXMm * MmToUnits;
        float oy = (float)t.OffsetYMm * MmToUnits;

        if (t.PrintArtwork)
        {
            var path = TemplateStore.ArtworkPath(t);
            if (path != null)
            {
                var art = ArtworkRenderer.Render(path, 300);
                if (art != null)
                    g.DrawImage(art, ox, oy,
                        (float)t.PageWidthMm * MmToUnits,
                        (float)t.PageHeightMm * MmToUnits);
            }
        }

        foreach (var f in t.Fields)
        {
            var data = f.Slot switch
            {
                FieldSlot.Left => sheet.Left,
                _              => sheet.Right
            };
            if (data == null) continue;

            if (hidden != null && hidden.Contains(f.Token)) continue;

            string text = data.Value(f.Token, f.StaticText);
            if (string.IsNullOrWhiteSpace(text)) continue;

            DrawField(g, f, text, ox, oy);
        }
    }

    private static void DrawField(Graphics g, TemplateField f, string text, float ox, float oy)
    {
        float w = (float)f.WidthMm * MmToUnits;
        float h = (float)(f.FontSize * 0.353 * 1.35) * MmToUnits;
        float ax = (float)f.XMm * MmToUnits + ox;
        float ay = (float)f.YMm * MmToUnits + oy;

        using var font = AppFonts.Create(f.FontName, (float)f.FontSize,
            f.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);

        using var sf = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.DirectionRightToLeft)
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None,
            Alignment = f.Align switch
            {
                FieldAlign.Center => StringAlignment.Center,
                FieldAlign.Left   => StringAlignment.Far,
                _                 => StringAlignment.Near
            }
        };

        var state = g.Save();
        g.TranslateTransform(ax, ay);
        if (f.Rotation != 0) g.RotateTransform(f.Rotation);
        g.DrawString(text, font, Brushes.Black, new RectangleF(0, 0, w, h), sf);
        g.Restore(state);
    }
}