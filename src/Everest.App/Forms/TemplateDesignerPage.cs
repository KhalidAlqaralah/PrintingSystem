using System.Diagnostics;
using Everest.App.Data;
using Everest.App.Models;
using Everest.App.Printing;

namespace Everest.App.Forms;

public class TemplateDesignerPage : UserControl
{
    private readonly TemplateCanvas _canvas = new() { Dock = DockStyle.Fill };

    private readonly ComboBox _category  = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly ComboBox _templates = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly Button _btnSave    = new() { Text = "حفظ" };
    private readonly Button _btnNew     = new() { Text = "جديد" };
    private readonly Button _btnDelete  = new() { Text = "حذف" };
    private readonly Button _btnFolder  = new() { Text = "فتح المجلد" };
    private readonly Button _btnDefaults = new() { Text = "استعادة الافتراضية" };
    private readonly Button _btnFont     = new() { Text = "تطبيق خط الشركة" };

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, RightToLeft = RightToLeft.No };

    private readonly TextBox _name = new();
    private readonly ComboBox _artwork = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _printArt = new() { Text = "طباعة التصميم مع البيانات", AutoSize = true,
                                                  Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _paper = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _pw = new() { Minimum = 20, Maximum = 1000, DecimalPlaces = 0, Value = 290 };
    private readonly NumericUpDown _ph = new() { Minimum = 20, Maximum = 1000, DecimalPlaces = 0, Value = 270 };
    private readonly NumericUpDown _offX = new() { Minimum = -50, Maximum = 50, DecimalPlaces = 1, Increment = 0.5M };
    private readonly NumericUpDown _offY = new() { Minimum = -50, Maximum = 50, DecimalPlaces = 1, Increment = 0.5M };

    private readonly ListBox _fields = new() { Font = AppTheme.UiFont, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _newToken = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _btnAddField = new() { Text = "إضافة" };
    private readonly Button _btnDupField = new() { Text = "تكرار" };
    private readonly Button _btnDelField = new() { Text = "حذف" };
    private readonly Button _btnMirror   = new() { Text = "نسخ اليمين إلى اليسار" };

    private readonly TextBox _fStatic = new();
    private readonly ComboBox _fSlot = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _fX = new() { Minimum = 0, Maximum = 1000, DecimalPlaces = 1, Increment = 0.5M };
    private readonly NumericUpDown _fY = new() { Minimum = 0, Maximum = 1000, DecimalPlaces = 1, Increment = 0.5M };
    private readonly NumericUpDown _fW = new() { Minimum = 5, Maximum = 500, DecimalPlaces = 1, Value = 60 };
    private readonly ComboBox _fFont = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _fSize = new() { Minimum = 4, Maximum = 72, DecimalPlaces = 1, Increment = 0.5M, Value = 10 };
    private readonly CheckBox _fBold = new() { Text = "عريض", AutoSize = true, Font = AppTheme.UiFont,
                                               RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _fAlign = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _fRot = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private TemplateDef? _t;
    private bool _suspend;

    public TemplateDesignerPage()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        RightToLeft = RightToLeft.Yes;

        DefaultTemplates.EnsureCreated();

        Controls.Add(BuildCanvasArea());
        Controls.Add(BuildSidePanel());

        _canvas.SelectionChanged += (_, _) => SyncFromCanvas();
        _canvas.FieldMoved       += (_, _) => { RefreshFieldList(); LoadFieldProps(); };

        _category.SelectedIndexChanged  += (_, _) => { RefreshTemplateList(); SelectFirst(); };
        _templates.SelectedIndexChanged += (_, _) => LoadSelectedTemplate();
        _btnNew.Click      += (_, _) => NewTemplate();
        _btnSave.Click     += (_, _) => SaveTemplate();
        _btnDelete.Click   += (_, _) => DeleteTemplate();
        _btnFolder.Click   += (_, _) => Process.Start("explorer.exe", AppPaths.FolderFor(CurrentCategory));
        _btnDefaults.Click += (_, _) => RestoreDefaults();
        _btnFont.Click += (_, _) =>
        {
            int n = DefaultTemplates.ApplyCompanyFont();
            ArtworkRenderer.Clear();
            LoadSelectedTemplate();
            MessageBox.Show($"تم تحديث {n} قالب إلى خط {AppFonts.ArabicFamily}.",
                "تم", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        _artwork.SelectedIndexChanged += (_, _) => ApplyArtwork();
        _printArt.CheckedChanged += (_, _) => { if (!_suspend && _t != null) _t.PrintArtwork = _printArt.Checked; };
        _paper.SelectedIndexChanged += (_, _) => ApplyPaper();
        _pw.ValueChanged   += (_, _) => { if (!_suspend && _t != null) { _t.PageWidthMm  = (double)_pw.Value; _canvas.Invalidate(); } };
        _ph.ValueChanged   += (_, _) => { if (!_suspend && _t != null) { _t.PageHeightMm = (double)_ph.Value; _canvas.Invalidate(); } };
        _offX.ValueChanged += (_, _) => { if (!_suspend && _t != null) _t.OffsetXMm = (double)_offX.Value; };
        _offY.ValueChanged += (_, _) => { if (!_suspend && _t != null) _t.OffsetYMm = (double)_offY.Value; };

        _fields.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend) return;
            _canvas.SelectedIndex = _fields.SelectedIndex;
            _canvas.Invalidate();
            LoadFieldProps();
        };

        _btnAddField.Click += (_, _) => AddField();
        _btnDupField.Click += (_, _) => DuplicateField();
        _btnDelField.Click += (_, _) => DeleteField();
        _btnMirror.Click   += (_, _) => MirrorToLeft();

        foreach (var c in new[] { _fSlot, _fFont, _fAlign, _fRot })
            c.SelectedIndexChanged += (_, _) => ApplyFieldProps();
        foreach (var c in new[] { _fX, _fY, _fW, _fSize })
            c.ValueChanged += (_, _) => ApplyFieldProps();
        _fBold.CheckedChanged += (_, _) => ApplyFieldProps();
        _fStatic.TextChanged  += (_, _) => ApplyFieldProps();

        foreach (var c in Categories.All) _category.Items.Add(c.Label);
        _category.SelectedIndex = 1;   // DoctorDouble
    }

    private TemplateCategory CurrentCategory =>
        Categories.All[Math.Max(0, _category.SelectedIndex)].Cat;

    // ---------- layout ----------

    private Control BuildCanvasArea()
    {
        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 16, 10, 16) };

        var title = new Label
        {
            Text = "مصمم القوالب",
            Font = AppTheme.TitleFont,
            ForeColor = AppTheme.Sidebar,
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleRight
        };

        var bar = new Panel { Dock = DockStyle.Top, Height = 46 };

        foreach (var b in new[] { _btnSave, _btnNew, _btnDelete, _btnFolder, _btnDefaults, _btnFont })
        {
            b.Height = 30;
            b.Font = AppTheme.UiFont;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.White;
            b.FlatAppearance.BorderColor = AppTheme.Line;
            b.Cursor = Cursors.Hand;
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(b);
        }

        _btnSave.BackColor = AppTheme.Accent;
        _btnSave.ForeColor = Color.White;
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnDelete.ForeColor = Color.Firebrick;

        _btnSave.Width = 78; _btnNew.Width = 66; _btnDelete.Width = 66;
        _btnFolder.Width = 96; _btnDefaults.Width = 136; _btnFont.Width = 118;

        foreach (var c in new Control[] { _category, _templates })
        {
            c.Font = AppTheme.UiFont;
            c.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(c);
        }

        bar.Resize += (_, _) =>
        {
            int x = bar.Width;
            _category.Location    = new Point(x - _category.Width - 2, 9);  x -= _category.Width + 8;
            _templates.Location   = new Point(x - _templates.Width, 9);     x -= _templates.Width + 14;
            _btnSave.Location     = new Point(x - _btnSave.Width, 8);       x -= _btnSave.Width + 6;
            _btnNew.Location      = new Point(x - _btnNew.Width, 8);        x -= _btnNew.Width + 6;
            _btnDelete.Location   = new Point(x - _btnDelete.Width, 8);     x -= _btnDelete.Width + 14;
            _btnFolder.Location   = new Point(x - _btnFolder.Width, 8);     x -= _btnFolder.Width + 6;
            _btnDefaults.Location = new Point(x - _btnDefaults.Width, 8); x -= _btnDefaults.Width + 6;
            _btnFont.Location     = new Point(x - _btnFont.Width, 8);
        };

        var hint = new Label
        {
            Text = "اسحب الحقل بالفأرة، أو حدّده واستخدم الأسهم — Shift = 5 مم، Ctrl = 0.1 مم",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.Gray,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleRight
        };

        host.Controls.Add(_canvas);
        host.Controls.Add(hint);
        host.Controls.Add(bar);
        host.Controls.Add(title);
        return host;
    }

    private Control BuildSidePanel()
    {
        var host = new Panel { Dock = DockStyle.Left, Width = 310, BackColor = AppTheme.Surface,
                               Padding = new Padding(10, 14, 10, 10) };

        _tabs.ItemSize = new Size(120, 28);
        _tabs.Font = AppTheme.UiFont;
        _tabs.TabPages.Add(BuildTemplateTab());
        _tabs.TabPages.Add(BuildFieldsTab());

        host.Controls.Add(_tabs);
        return host;
    }

    private static Label Cap(string text, int y) => new()
    {
        Text = text, Font = AppTheme.UiFont, ForeColor = Color.DimGray,
        Location = new Point(10, y), Size = new Size(270, 18),
        TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
    };

    private TabPage BuildTemplateTab()
    {
        var p = new TabPage("القالب") { BackColor = AppTheme.Surface, AutoScroll = true,
                                        RightToLeft = RightToLeft.Yes };
        int y = 12;

        void Full(Control c)
        {
            c.SetBounds(10, y, 270, c.Height);
            c.Font = AppTheme.UiFont;
            c.RightToLeft = RightToLeft.Yes;
            p.Controls.Add(c);
            y += c.Height + 10;
        }

        void Half(Control a, Control b)
        {
            a.SetBounds(149, y, 131, a.Height);
            b.SetBounds(10, y, 131, b.Height);
            a.Font = b.Font = AppTheme.UiFont;
            p.Controls.Add(a); p.Controls.Add(b);
            y += a.Height + 10;
        }

        p.Controls.Add(Cap("الاسم", y)); y += 20;
        Full(_name);

        p.Controls.Add(Cap("ملف التصميم (PDF / صورة)", y)); y += 20;
        Full(_artwork);

        _printArt.Location = new Point(10, y);
        p.Controls.Add(_printArt);
        y += 26;

        p.Controls.Add(new Label
        {
            Text = "إذا كان الورق مطبوعاً مسبقاً اترك الخيار فارغاً — سيظهر التصميم على الشاشة فقط.",
            Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Gray,
            Location = new Point(10, y), Size = new Size(270, 44),
            TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.Yes
        });
        y += 50;

        p.Controls.Add(Cap("حجم الورق", y)); y += 20;
        Full(_paper);
        _paper.Items.AddRange(new object[]
        {
            "290 × 270 (الشائع)", "145 × 270 (نصف)", "A4 عرضي (297×210)",
            "A4 طولي (210×297)", "A5 طولي (148×210)", "مخصص"
        });

        p.Controls.Add(Cap("العرض / الارتفاع (مم)", y)); y += 20;
        Half(_pw, _ph);

        y += 6;
        p.Controls.Add(Cap("إزاحة الطباعة — أفقي / عمودي (مم)", y)); y += 20;
        Half(_offX, _offY);

        p.Controls.Add(new Label
        {
            Text = "الإزاحة تحرّك كل المحتوى على الورق عند الطباعة فقط.",
            Font = new Font("Segoe UI", 8.5F), ForeColor = Color.Gray,
            Location = new Point(10, y), Size = new Size(270, 34),
            TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.Yes
        });

        return p;
    }

    private TabPage BuildFieldsTab()
    {
        var p = new TabPage("الحقول") { BackColor = AppTheme.Surface, AutoScroll = true,
                                        RightToLeft = RightToLeft.Yes };
        int y = 10;

        void Full(Control c)
        {
            c.SetBounds(10, y, 270, c.Height);
            c.Font = AppTheme.UiFont;
            c.RightToLeft = RightToLeft.Yes;
            p.Controls.Add(c);
            y += c.Height + 10;
        }

        void Half(Control a, Control b)
        {
            a.SetBounds(149, y, 131, a.Height);
            b.SetBounds(10, y, 131, b.Height);
            a.Font = b.Font = AppTheme.UiFont;
            p.Controls.Add(a); p.Controls.Add(b);
            y += a.Height + 10;
        }

        _fields.SetBounds(10, y, 270, 132);
        p.Controls.Add(_fields);
        y += 140;

        p.Controls.Add(Cap("إضافة حقل", y)); y += 20;
        Full(_newToken);
        foreach (var t in Tokens.All) _newToken.Items.Add(t.Label);
        _newToken.SelectedIndex = 0;

        foreach (var b in new[] { _btnAddField, _btnDupField, _btnDelField, _btnMirror })
        {
            b.Font = AppTheme.UiFont;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.White;
            b.FlatAppearance.BorderColor = AppTheme.Line;
            b.Cursor = Cursors.Hand;
            p.Controls.Add(b);
        }

        _btnAddField.SetBounds(196, y, 84, 30);
        _btnDupField.SetBounds(104, y, 84, 30);
        _btnDelField.SetBounds(10, y, 86, 30);
        _btnDelField.ForeColor = Color.Firebrick;
        y += 38;

        _btnMirror.SetBounds(10, y, 270, 30);
        y += 42;

        p.Controls.Add(new Label
        {
            Text = "خصائص الحقل", Font = AppTheme.UiFontBold, ForeColor = AppTheme.Sidebar,
            Location = new Point(10, y), Size = new Size(270, 22),
            TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes
        });
        y += 28;

        p.Controls.Add(Cap("النص الثابت", y)); y += 20;
        Full(_fStatic);

        p.Controls.Add(Cap("الجهة", y)); y += 20;
        Full(_fSlot);
        _fSlot.Items.AddRange(new object[] { "يمين", "يسار", "الاثنتان" });

        p.Controls.Add(Cap("X / Y (مم)", y)); y += 20;
        Half(_fX, _fY);

        p.Controls.Add(Cap("العرض (مم)", y)); y += 20;
        Full(_fW);

        p.Controls.Add(Cap("الخط", y)); y += 20;
        Full(_fFont);
        foreach (var n in AppFonts.AvailableFamilies()) _fFont.Items.Add(n);
        p.Controls.Add(Cap("الحجم / المحاذاة", y)); y += 20;
        Half(_fSize, _fAlign);
        _fAlign.Items.AddRange(new object[] { "يمين", "وسط", "يسار" });

        p.Controls.Add(Cap("الدوران", y)); y += 20;
        Full(_fRot);
        _fRot.Items.AddRange(new object[] { "0°", "90°", "270°" });

        _fBold.Location = new Point(10, y);
        p.Controls.Add(_fBold);
        y += 34;

        return p;
    }

    // ---------- template ----------

    private void RefreshTemplateList()
    {
        _suspend = true;
        _templates.Items.Clear();
        foreach (var n in TemplateStore.Names(CurrentCategory)) _templates.Items.Add(n);
        _suspend = false;
    }

    private void SelectFirst()
    {
        OfferOrphans();
        RefreshTemplateList();

        if (_templates.Items.Count > 0)
        {
            _suspend = true;
            _templates.SelectedIndex = 0;
            _suspend = false;
            LoadSelectedTemplate();
        }
        else NewTemplate();
    }

    private void OfferOrphans()
    {
        var orphans = TemplateStore.OrphanArtwork(CurrentCategory);
        if (orphans.Count == 0) return;

        var ask = MessageBox.Show(
            $"وُجد {orphans.Count} ملف تصميم بدون إعدادات حقول في هذا المجلد:\n\n" +
            string.Join("\n", orphans.Take(6)) +
            "\n\nهل تريد إنشاء قالب لكل منها بنفس حقول القالب الافتراضي؟",
            "ملفات تصميم جديدة", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (ask != DialogResult.Yes) return;

        var basis = TemplateStore.LoadCategory(CurrentCategory).FirstOrDefault();

        foreach (var file in orphans)
        {
            var t = new TemplateDef
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Category = CurrentCategory,
                PdfFile = file,
                PrintArtwork = basis?.PrintArtwork ?? false,
                PageWidthMm  = basis?.PageWidthMm  ?? DefaultTemplates.SheetW,
                PageHeightMm = basis?.PageHeightMm ?? DefaultTemplates.SheetH,
                Fields = basis?.Fields.Select(f => f.Clone()).ToList() ?? new List<TemplateField>()
            };
            TemplateStore.Save(t);
        }
    }

    private void LoadSelectedTemplate()
    {
        if (_suspend) return;
        var name = _templates.SelectedItem?.ToString();
        if (name == null) return;
        var t = TemplateStore.Load(CurrentCategory, name);
        if (t == null) return;
        _t = t;
        BindTemplate();
    }

    private void BindTemplate()
    {
        if (_t == null) return;
        _suspend = true;

        _name.Text = _t.Name;
        _printArt.Checked = _t.PrintArtwork;
        _pw.Value = (decimal)Math.Clamp(_t.PageWidthMm,  (double)_pw.Minimum, (double)_pw.Maximum);
        _ph.Value = (decimal)Math.Clamp(_t.PageHeightMm, (double)_ph.Minimum, (double)_ph.Maximum);
        _offX.Value = (decimal)Math.Clamp(_t.OffsetXMm, -50, 50);
        _offY.Value = (decimal)Math.Clamp(_t.OffsetYMm, -50, 50);
        _paper.SelectedIndex = PaperIndex(_t.PageWidthMm, _t.PageHeightMm);

        RefreshArtworkList();

        _canvas.Template = _t;
        _canvas.SelectedIndex = _t.Fields.Count > 0 ? 0 : -1;

        _suspend = false;

        LoadArtworkImage();
        RefreshFieldList();
        LoadFieldProps();
        _canvas.Invalidate();
    }

    private void RefreshArtworkList()
    {
        _artwork.Items.Clear();
        _artwork.Items.Add("(بدون تصميم)");

        var dir = AppPaths.FolderFor(CurrentCategory);
        foreach (var f in Directory.GetFiles(dir)
                     .Where(f => Path.GetExtension(f).ToLowerInvariant()
                                 is ".pdf" or ".png" or ".jpg" or ".jpeg")
                     .Select(Path.GetFileName).OrderBy(n => n))
            _artwork.Items.Add(f!);

        int i = _t != null && _t.PdfFile.Length > 0 ? _artwork.Items.IndexOf(_t.PdfFile) : 0;
        _artwork.SelectedIndex = i >= 0 ? i : 0;
    }

    private void ApplyArtwork()
    {
        if (_suspend || _t == null) return;
        _t.PdfFile = _artwork.SelectedIndex <= 0 ? "" : _artwork.SelectedItem!.ToString()!;
        LoadArtworkImage();
        _canvas.Invalidate();
    }

    private void LoadArtworkImage()
    {
        if (_t == null) { _canvas.Artwork = null; return; }

        var path = TemplateStore.ArtworkPath(_t);
        if (path == null) { _canvas.Artwork = null; return; }

        Cursor = Cursors.WaitCursor;
        _canvas.Artwork = ArtworkRenderer.Render(path, 150);
        Cursor = Cursors.Default;

        if (_canvas.Artwork == null && _t.PdfFile.Length > 0)
            MessageBox.Show($"تعذر عرض الملف \"{_t.PdfFile}\".\n\nتأكد أنه ملف PDF أو صورة صالحة.",
                "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static int PaperIndex(double w, double h) => (w, h) switch
    {
        (290, 270) => 0,
        (145, 270) => 1,
        (297, 210) => 2,
        (210, 297) => 3,
        (148, 210) => 4,
        _          => 5
    };

    private void ApplyPaper()
    {
        if (_suspend || _t == null) return;
        (double w, double h)? s = _paper.SelectedIndex switch
        {
            0 => (290, 270),
            1 => (145, 270),
            2 => (297, 210),
            3 => (210, 297),
            4 => (148, 210),
            _ => null
        };
        if (s == null) return;

        _suspend = true;
        _t.PageWidthMm = s.Value.w;
        _t.PageHeightMm = s.Value.h;
        _pw.Value = (decimal)s.Value.w;
        _ph.Value = (decimal)s.Value.h;
        _suspend = false;
        _canvas.Invalidate();
    }

    private void NewTemplate()
    {
        bool half = CurrentCategory is TemplateCategory.DoctorSingle or TemplateCategory.HospitalSingle
                                    or TemplateCategory.Sample;        bool sample = CurrentCategory == TemplateCategory.Sample;

        _t = new TemplateDef
        {
            Name = "قالب " + DateTime.Now.ToString("HHmmss"),
            Category = CurrentCategory,
            PageWidthMm  = sample ? 210 : half ? 145 : DefaultTemplates.SheetW,
            PageHeightMm = sample ? 297 : DefaultTemplates.SheetH
        };

        _suspend = true;
        _templates.SelectedIndex = -1;
        _suspend = false;

        BindTemplate();
    }

    private void SaveTemplate()
    {
        if (_t == null) return;

        string name = _name.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show("أدخل اسماً للقالب.", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _tabs.SelectedIndex = 0; _name.Focus();
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show("اسم القالب يحتوي على رموز غير مسموحة.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (name != _t.Name && TemplateStore.Exists(CurrentCategory, name))
        {
            var ask = MessageBox.Show("يوجد قالب بنفس الاسم. استبداله؟", "تأكيد",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ask != DialogResult.Yes) return;
        }

        _t.Name = name;
        _t.Category = CurrentCategory;

        try
        {
            TemplateStore.Save(_t);
            RefreshTemplateList();
            _suspend = true;
            _templates.SelectedItem = name;
            _suspend = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر الحفظ:\n\n" + ex.Message, "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeleteTemplate()
    {
        var name = _templates.SelectedItem?.ToString();
        if (name == null) return;

        var ask = MessageBox.Show($"حذف إعدادات القالب \"{name}\"؟\n\nملف التصميم يبقى في المجلد.",
            "تأكيد الحذف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (ask != DialogResult.Yes) return;

        TemplateStore.Delete(CurrentCategory, name);
        RefreshTemplateList();
        if (_templates.Items.Count > 0) _templates.SelectedIndex = 0; else NewTemplate();
    }

    private void RestoreDefaults()
    {
        var ask = MessageBox.Show(
            "سيتم حذف جميع إعدادات القوالب وإعادة إنشاء القوالب الافتراضية.\n\n" +
            "ملفات التصميم (PDF) تبقى في مجلداتها. المتابعة؟",
            "استعادة الافتراضية", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (ask != DialogResult.Yes) return;

        try
        {
            DefaultTemplates.HardReset();
            ArtworkRenderer.Clear();

            _t = null;
            _canvas.Template = null;
            _canvas.Artwork = null;

            RefreshTemplateList();

            if (_templates.Items.Count > 0)
            {
                _suspend = true;
                _templates.SelectedIndex = 0;
                _suspend = false;
                LoadSelectedTemplate();       // forced — SelectedIndexChanged may not fire
            }
            else NewTemplate();

            MessageBox.Show("تمت الاستعادة.", "تم",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذرت الاستعادة:\n\n" + ex.Message, "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------- fields ----------

    private void RefreshFieldList()
    {
        if (_t == null) return;
        _suspend = true;
        int sel = _canvas.SelectedIndex;
        _fields.Items.Clear();
        foreach (var f in _t.Fields)
        {
            string side = _t.TwoUp ? f.Slot switch
            {
                FieldSlot.Right => "يمين | ",
                FieldSlot.Left  => "يسار | ",
                _               => "كلاهما | "
            } : "";
            string label = f.Token == "Static" && f.StaticText.Length > 0
                ? $"\"{Truncate(f.StaticText, 16)}\""
                : Tokens.Label(f.Token);
            _fields.Items.Add($"{side}{label}   ({f.XMm:0.#}, {f.YMm:0.#})");
        }
        if (sel >= 0 && sel < _fields.Items.Count) _fields.SelectedIndex = sel;
        _suspend = false;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";

    private void SyncFromCanvas()
    {
        _suspend = true;
        if (_canvas.SelectedIndex >= 0 && _canvas.SelectedIndex < _fields.Items.Count)
            _fields.SelectedIndex = _canvas.SelectedIndex;
        else
            _fields.ClearSelected();
        _suspend = false;
        LoadFieldProps();
    }

    private TemplateField? Current =>
        _t != null && _canvas.SelectedIndex >= 0 && _canvas.SelectedIndex < _t.Fields.Count
            ? _t.Fields[_canvas.SelectedIndex] : null;

    private void LoadFieldProps()
    {
        var f = Current;
        bool on = f != null;

        foreach (var c in new Control[] { _fStatic, _fSlot, _fX, _fY, _fW,
                                          _fFont, _fSize, _fBold, _fAlign, _fRot })
            c.Enabled = on;

        if (f == null) return;

        _suspend = true;
        _fStatic.Text = f.StaticText;
        _fStatic.Enabled = f.Token == "Static";
        _fSlot.SelectedIndex = (int)f.Slot;
        _fX.Value = (decimal)Math.Clamp(f.XMm, 0, (double)_fX.Maximum);
        _fY.Value = (decimal)Math.Clamp(f.YMm, 0, (double)_fY.Maximum);
        _fW.Value = (decimal)Math.Clamp(f.WidthMm, (double)_fW.Minimum, (double)_fW.Maximum);
        _fFont.SelectedItem = f.FontName;
        if (_fFont.SelectedIndex < 0)
        {
            int i = _fFont.Items.IndexOf(AppFonts.PrintFamily);
            _fFont.SelectedIndex = i >= 0 ? i : (_fFont.Items.Count > 0 ? 0 : -1);
        }	
        _fSize.Value = (decimal)Math.Clamp(f.FontSize, 4, 72);
        _fBold.Checked = f.Bold;
        _fAlign.SelectedIndex = (int)f.Align;
        _fRot.SelectedIndex = f.Rotation switch { 90 => 1, 270 => 2, _ => 0 };
        _suspend = false;
    }

    private void ApplyFieldProps()
    {
        if (_suspend) return;
        var f = Current;
        if (f == null) return;

        f.StaticText = _fStatic.Text;
        f.Slot     = (FieldSlot)Math.Max(0, _fSlot.SelectedIndex);
        f.XMm      = (double)_fX.Value;
        f.YMm      = (double)_fY.Value;
        f.WidthMm  = (double)_fW.Value;
        f.FontName = _fFont.SelectedItem?.ToString() ?? AppFonts.PrintFamily;
        f.FontSize = (double)_fSize.Value;
        f.Bold     = _fBold.Checked;
        f.Align    = (FieldAlign)Math.Max(0, _fAlign.SelectedIndex);
        f.Rotation = _fRot.SelectedIndex switch { 1 => 90, 2 => 270, _ => 0 };

        RefreshFieldList();
        _canvas.Invalidate();
    }

    private void AddField()
    {
        if (_t == null) return;

        string token = Tokens.All[Math.Max(0, _newToken.SelectedIndex)].Token;

        _t.Fields.Add(new TemplateField
        {
            Token = token,
            StaticText = token == "Static" ? "نص ثابت" : "",
            Slot = _t.TwoUp ? FieldSlot.Right : FieldSlot.Both,
            XMm = Math.Round(_t.PageWidthMm * (_t.TwoUp ? 0.56 : 0.12), 1),
            YMm = 20,
            WidthMm = Math.Round(_t.PageWidthMm * (_t.TwoUp ? 0.35 : 0.75), 1),
            FontName = AppFonts.PrintFamily,
            FontSize = 10,
            Align = FieldAlign.Right
        });

        _canvas.SelectedIndex = _t.Fields.Count - 1;
        RefreshFieldList();
        LoadFieldProps();
        _canvas.Invalidate();
    }

    private void DuplicateField()
    {
        var f = Current;
        if (_t == null || f == null) return;

        var c = f.Clone();
        c.YMm = Math.Round(Math.Min(c.YMm + 8, _t.PageHeightMm - 1), 1);
        _t.Fields.Add(c);

        _canvas.SelectedIndex = _t.Fields.Count - 1;
        RefreshFieldList();
        LoadFieldProps();
        _canvas.Invalidate();
    }

    private void DeleteField()
    {
        if (_t == null || _canvas.SelectedIndex < 0) return;
        _t.Fields.RemoveAt(_canvas.SelectedIndex);
        _canvas.SelectedIndex = _t.Fields.Count > 0 ? 0 : -1;
        RefreshFieldList();
        LoadFieldProps();
        _canvas.Invalidate();
    }

    private void MirrorToLeft()
    {
        if (_t == null) return;
        if (!_t.TwoUp)
        {
            MessageBox.Show("هذه الفئة ليست بوصفتين في الصفحة.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var rights = _t.Fields.Where(f => f.Slot == FieldSlot.Right).ToList();
        if (rights.Count == 0)
        {
            MessageBox.Show("لا توجد حقول على الجهة اليمنى.", "تنبيه",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _t.Fields.RemoveAll(f => f.Slot == FieldSlot.Left);

        double half = _t.PageWidthMm / 2.0;
        foreach (var r in rights)
        {
            var c = r.Clone();
            c.Slot = FieldSlot.Left;
            c.XMm = Math.Round(Math.Max(0, r.XMm - half), 1);
            _t.Fields.Add(c);
        }

        RefreshFieldList();
        _canvas.Invalidate();
    }
}