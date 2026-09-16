using System.Drawing.Drawing2D;
using Everest.App.Models;
using Everest.App.Printing;

namespace Everest.App.Forms;

public class TemplateCanvas : Panel
{
    public TemplateDef? Template { get; set; }
    public int SelectedIndex { get; set; } = -1;
    public Bitmap? Artwork { get; set; }

    public event EventHandler? FieldMoved;
    public event EventHandler? SelectionChanged;

    private float _scale = 1f;
    private float _originX, _originY;
    private bool _dragging;
    private PointF _grabMm;

    public TemplateCanvas()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(228, 233, 238);
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    private void Recalc()
    {
        if (Template == null) return;
        float availW = Width - 48, availH = Height - 48;
        _scale = Math.Min(availW / (float)Template.PageWidthMm,
                          availH / (float)Template.PageHeightMm);
        if (_scale <= 0.01f) _scale = 0.01f;
        _originX = (Width  - (float)Template.PageWidthMm  * _scale) / 2f;
        _originY = (Height - (float)Template.PageHeightMm * _scale) / 2f;
    }

    private static double LineMm(TemplateField f) => f.FontSize * 0.353 * 1.35;

    private RectangleF BoxOf(TemplateField f)
    {
        float w = (float)f.WidthMm * _scale;
        float h = (float)LineMm(f) * _scale;
        float x = _originX + (float)f.XMm * _scale;
        float y = _originY + (float)f.YMm * _scale;

        if (f.Rotation == 90)  return new RectangleF(x - h, y, h, w);
        if (f.Rotation == 270) return new RectangleF(x, y - w, h, w);
        return new RectangleF(x, y, w, h);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Recalc(); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (Template == null)
        {
            using var sfNone = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("لا يوجد قالب محدد", AppTheme.UiFont, Brushes.Gray, ClientRectangle, sfNone);
            return;
        }

        Recalc();

        float pw = (float)Template.PageWidthMm  * _scale;
        float ph = (float)Template.PageHeightMm * _scale;
        var page = new RectangleF(_originX, _originY, pw, ph);

        using (var shadow = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
            g.FillRectangle(shadow, _originX + 4, _originY + 4, pw, ph);

        g.FillRectangle(Brushes.White, page);

        if (Artwork != null)
            g.DrawImage(Artwork, page);

        g.DrawRectangle(Pens.Silver, page.X, page.Y, page.Width, page.Height);

        if (Template.TwoUp)
        {
            using var dash = new Pen(Color.FromArgb(150, 70, 130, 180), 1.4f)
            { DashStyle = DashStyle.Dash };
            float mid = _originX + pw / 2f;
            g.DrawLine(dash, mid, _originY, mid, _originY + ph);

            using var lbl = new Font("Segoe UI", 8F);
            using var sfC = new StringFormat { Alignment = StringAlignment.Center };
            using var back = new SolidBrush(Color.FromArgb(190, 255, 255, 255));
            g.FillRectangle(back, mid, _originY + 2, pw / 2f, 15);
            g.FillRectangle(back, _originX, _originY + 2, pw / 2f, 15);
            g.DrawString("يمين", lbl, Brushes.SteelBlue,
                new RectangleF(mid, _originY + 2, pw / 2f, 15), sfC);
            g.DrawString("يسار", lbl, Brushes.SteelBlue,
                new RectangleF(_originX, _originY + 2, pw / 2f, 15), sfC);
        }

        for (int i = 0; i < Template.Fields.Count; i++)
            DrawField(g, Template.Fields[i], i == SelectedIndex);
    }

    private void DrawField(Graphics g, TemplateField f, bool selected)
    {
        float uw = (float)f.WidthMm * _scale;
        float uh = (float)LineMm(f) * _scale;
        float ax = _originX + (float)f.XMm * _scale;
        float ay = _originY + (float)f.YMm * _scale;

        float fontPx = (float)(f.FontSize * 0.353) * _scale;
        if (fontPx < 3f) fontPx = 3f;

        string text = Tokens.Sample(f.Token, f.StaticText);

        using var font = AppFonts.Create(f.FontName, fontPx,
            f.Bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);

        using var sf = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.DirectionRightToLeft)
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
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

        var box = new RectangleF(0, 0, uw, uh);

        using (var fill = new SolidBrush(selected ? Color.FromArgb(56, 62, 138, 178)
                                                  : Color.FromArgb(22, 110, 110, 110)))
            g.FillRectangle(fill, box);

        using (var pen = new Pen(selected ? AppTheme.Accent : Color.Silver, selected ? 1.8f : 0.8f))
            g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

        g.DrawString(text, font, Brushes.Black, box, sf);
        g.Restore(state);
    }

    private PointF ToMm(int px, int py) =>
        new((px - _originX) / _scale, (py - _originY) / _scale);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (Template == null) return;

        for (int i = Template.Fields.Count - 1; i >= 0; i--)
        {
            if (BoxOf(Template.Fields[i]).Contains(e.Location))
            {
                SelectedIndex = i;
                var mm = ToMm(e.X, e.Y);
                _grabMm = new PointF(mm.X - (float)Template.Fields[i].XMm,
                                     mm.Y - (float)Template.Fields[i].YMm);
                _dragging = true;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }
        }

        SelectedIndex = -1;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || Template == null || SelectedIndex < 0) return;

        var mm = ToMm(e.X, e.Y);
        var f = Template.Fields[SelectedIndex];
        f.XMm = Math.Round(Math.Clamp(mm.X - _grabMm.X, 0, Template.PageWidthMm  - 1), 1);
        f.YMm = Math.Round(Math.Clamp(mm.Y - _grabMm.Y, 0, Template.PageHeightMm - 1), 1);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging) { _dragging = false; FieldMoved?.Invoke(this, EventArgs.Empty); }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Template == null || SelectedIndex < 0) return;

        double step = e.Shift ? 5 : e.Control ? 0.1 : 1;
        var f = Template.Fields[SelectedIndex];

        switch (e.KeyCode)
        {
            case Keys.Left:  f.XMm -= step; break;
            case Keys.Right: f.XMm += step; break;
            case Keys.Up:    f.YMm -= step; break;
            case Keys.Down:  f.YMm += step; break;
            default: return;
        }

        f.XMm = Math.Round(Math.Clamp(f.XMm, 0, Template.PageWidthMm  - 1), 1);
        f.YMm = Math.Round(Math.Clamp(f.YMm, 0, Template.PageHeightMm - 1), 1);
        e.Handled = true;
        Invalidate();
        FieldMoved?.Invoke(this, EventArgs.Empty);
    }
}